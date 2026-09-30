using System.Text;
using System.Text.Json;
using ArrowOut.Data;
using ArrowOut.Game;
using ArrowOut.Game.Solving;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using ArrowOut.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Tests.Services;

public class LevelAdminServiceTests : IDisposable
{
    private const string ValidArrows = """[{"x":1,"y":1,"direction":"Right","length":1},{"x":3,"y":1,"direction":"Right","length":2}]""";
    private const string JammedArrows = """[{"x":0,"y":1,"direction":"Right","length":1},{"x":3,"y":1,"direction":"Left","length":1}]""";

    private readonly ApplicationDbContext _db = TestDb.Create();

    private LevelAdminService CreateService() => new(_db, new LevelDesignValidator(new GreedySolver()));

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static LevelInputModel Input(int number = 1, string arrows = ValidArrows) => new()
    {
        Number = number,
        Name = "Test level",
        Width = 5,
        Height = 3,
        MaxLives = 3,
        Difficulty = Difficulty.Easy,
        IsPublished = true,
        ArrowsJson = arrows,
    };

    [Fact]
    public async Task Create_PersistsLevelAndArrows()
    {
        var id = await CreateService().CreateAsync(Input());

        var level = await _db.Levels.Include(l => l.Arrows).SingleAsync(l => l.Id == id);
        Assert.Equal(2, level.Arrows.Count);
        Assert.Equal("Test level", level.Name);
    }

    [Fact]
    public async Task Create_BentArrow_PersistsItsPath()
    {
        const string bent = """[{"x":1,"y":0,"direction":"Up","length":3,"cells":[{"x":1,"y":0},{"x":1,"y":1},{"x":2,"y":1}]}]""";

        var id = await CreateService().CreateAsync(Input(arrows: bent));
        var arrow = await _db.Arrows.SingleAsync(a => a.LevelId == id);
        var edit = await CreateService().GetForEditAsync(id);

        Assert.Equal("1,0;1,1;2,1", arrow.Path);
        Assert.Contains("\"cells\"", edit.ArrowsJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{"x":1,"y":1,"direction":"Right","cells":[{"x":1,"y":1},{"x":1,"y":2}]}]""")]
    [InlineData("""[{"x":1,"y":1,"direction":"Up","cells":[{"x":1,"y":1},{"x":3,"y":1}]}]""")]
    [InlineData("""[{"x":1,"y":1,"direction":"Up","cells":[{"x":1,"y":1},{"x":1,"y":99}]}]""")]
    public async Task Create_BrokenPath_IsRejected(string arrows)
    {
        await Assert.ThrowsAsync<InvalidLevelDesignException>(() => CreateService().CreateAsync(Input(arrows: arrows)));
    }

    [Fact]
    public async Task Create_UnsolvableDesign_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<InvalidLevelDesignException>(() => CreateService().CreateAsync(Input(arrows: JammedArrows)));

        Assert.Contains(ex.Errors, e => e.Contains("cannot be cleared", StringComparison.Ordinal));
        Assert.Empty(_db.Levels);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""[{"x":1,"y":1,"direction":"Right","length":3}]""")]
    [InlineData("""[{"x":1,"y":1,"direction":"Sideways","length":1}]""")]
    [InlineData("""[{"x":1,"y":1,"direction":"Up","length":42}]""")]
    public async Task Create_MalformedArrows_AreRejected(string arrows)
    {
        await Assert.ThrowsAsync<InvalidLevelDesignException>(() => CreateService().CreateAsync(Input(arrows: arrows)));
    }

    [Fact]
    public async Task Create_DuplicateNumber_IsRejected()
    {
        var service = CreateService();
        await service.CreateAsync(Input(7));

        await Assert.ThrowsAsync<DuplicateLevelNumberException>(() => service.CreateAsync(Input(7)));
    }

    [Fact]
    public async Task Update_LayoutChange_ResetsProgress()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        var level = (await TestDb.AddLevelsAsync(_db, TestDb.QueueLevel(1)))[0];
        await TestDb.CompleteAsync(_db, TestDb.PlayerId, level);

        await CreateService().UpdateAsync(level.Id, Input(1));

        Assert.Empty(_db.PlayerProgress);
        Assert.Equal(2, _db.Arrows.Count(a => a.LevelId == level.Id));
    }

    [Fact]
    public async Task Update_MetadataOnly_KeepsProgress()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        var level = (await TestDb.AddLevelsAsync(_db, TestDb.QueueLevel(1)))[0];
        await TestDb.CompleteAsync(_db, TestDb.PlayerId, level);
        var sameLayout = await CreateService().GetForEditAsync(level.Id);
        sameLayout.Name = "Renamed";

        await CreateService().UpdateAsync(level.Id, sameLayout);

        Assert.Single(_db.PlayerProgress);
        Assert.Equal("Renamed", (await _db.Levels.SingleAsync()).Name);
    }

    [Fact]
    public async Task Update_MissingLevel_Throws404()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService().UpdateAsync(999, Input()));
    }

    [Fact]
    public async Task GetPaged_FiltersAndSorts()
    {
        await TestDb.AddLevelsAsync(_db,
            TestDb.QueueLevel(1, name: "Alpha"),
            TestDb.QueueLevel(2, published: false, name: "Bravo"),
            TestDb.QueueLevel(3, name: "Charlie"));

        var drafts = await CreateService().GetPagedAsync(new AdminLevelQuery { IsPublished = false });
        var desc = await CreateService().GetPagedAsync(new AdminLevelQuery { Sort = AdminLevelSort.NumberDesc, PageSize = 2 });

        Assert.Equal([2], drafts.Items.Select(i => i.Number));
        Assert.Equal([3, 2], desc.Items.Select(i => i.Number));
        Assert.Equal(2, desc.TotalPages);
    }

    [Fact]
    public async Task ExportThenImport_RoundTrips()
    {
        var service = CreateService();
        var id = await service.CreateAsync(Input(5));
        var (fileName, content) = await service.ExportAsync(id);

        // Change the number so the import doesn't clash with the original.
        var document = JsonSerializer.Deserialize<LevelFile>(content, LevelFile.JsonOptions)!;
        document.Number = 6;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, LevelFile.JsonOptions);

        var importedId = await service.ImportAsync(new MemoryStream(bytes));
        var imported = await _db.Levels.Include(l => l.Arrows).SingleAsync(l => l.Id == importedId);

        Assert.Equal("arrowout-level-0005.json", fileName);
        Assert.False(imported.IsPublished);
        Assert.Equal(2, imported.Arrows.Count);
    }

    [Fact]
    public async Task Import_WrongFormat_IsRejected()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"format":"something-else","version":1}""");

        await Assert.ThrowsAsync<InvalidLevelDesignException>(() => CreateService().ImportAsync(new MemoryStream(bytes)));
    }

    [Fact]
    public async Task Import_ScriptInName_FailsValidation()
    {
        var file = new LevelFile
        {
            Number = 9, Name = "<script>alert(1)</script>", Width = 5, Height = 3,
            Arrows = [new ArrowInputModel { X = 1, Y = 1, Direction = Direction.Up }],
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, LevelFile.JsonOptions);

        await Assert.ThrowsAsync<InvalidLevelDesignException>(() => CreateService().ImportAsync(new MemoryStream(bytes)));
    }

    [Fact]
    public void Generate_ProducesSolvableLayout()
    {
        var service = CreateService();

        var arrows = service.Generate(new GenerateLevelRequest { Width = 8, Height = 8, Fill = 1.0, MaxLength = 6 });
        var report = service.Analyze(8, 8, JsonSerializer.Serialize(arrows, LevelDesignValidator.JsonOptions));

        Assert.True(report.IsValid, string.Join(" ", report.Errors));
        Assert.True(report.IsSolvable);
        Assert.Equal(64, arrows.Sum(a => a.Cells?.Count ?? a.Length));
    }

    [Fact]
    public async Task IsNumberAvailable_ExcludesTheLevelBeingEdited()
    {
        var id = await CreateService().CreateAsync(Input(3));

        Assert.False(await CreateService().IsNumberAvailableAsync(3, null));
        Assert.True(await CreateService().IsNumberAvailableAsync(3, id));
        Assert.True(await CreateService().IsNumberAvailableAsync(4, null));
    }
}
