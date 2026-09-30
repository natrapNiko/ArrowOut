using ArrowOut.Data;
using ArrowOut.Game;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using ArrowOut.Tests.TestInfrastructure;

namespace ArrowOut.Tests.Services;

public class LevelServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();

    private LevelService CreateService() => new(_db, new LevelAccessGuard(_db));

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetLevels_PagesAndComputesStatuses()
    {
        await SeedAsync(30);
        var levels = _db.Levels.OrderBy(l => l.Number).ToList();
        await TestDb.CompleteAsync(_db, TestDb.PlayerId, levels[0]);

        var page = await CreateService().GetLevelsAsync(new LevelQuery { Page = 1, PageSize = 10 }, TestDb.PlayerId, false);

        Assert.Equal(30, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(LevelStatus.Completed, page.Items[0].Status);
        Assert.Equal(3, page.Items[0].Stars);
        Assert.Equal(LevelStatus.Unlocked, page.Items[1].Status);
        Assert.Equal(LevelStatus.Locked, page.Items[2].Status);
    }

    [Fact]
    public async Task GetLevels_ExcludesDrafts()
    {
        await SeedAsync(3);
        await TestDb.AddLevelsAsync(_db, TestDb.QueueLevel(99, published: false));

        var page = await CreateService().GetLevelsAsync(new LevelQuery(), TestDb.PlayerId, false);

        Assert.DoesNotContain(page.Items, l => l.Number == 99);
    }

    [Fact]
    public async Task GetLevels_SearchesByNameOrNumber()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        await TestDb.AddLevelsAsync(_db,
            TestDb.QueueLevel(1, name: "Quiet Knot"),
            TestDb.QueueLevel(2, name: "Busy Harbour"),
            TestDb.QueueLevel(12, name: "Another Knot"));

        var byName = await CreateService().GetLevelsAsync(new LevelQuery { Search = "Knot" }, TestDb.PlayerId, false);
        var byNumber = await CreateService().GetLevelsAsync(new LevelQuery { Search = "2" }, TestDb.PlayerId, false);

        Assert.Equal([1, 12], byName.Items.Select(i => i.Number));
        Assert.Equal([2], byNumber.Items.Select(i => i.Number));
    }

    [Fact]
    public async Task GetLevels_FiltersByDifficultyAndStatus()
    {
        await SeedAsync(5);
        var hard = TestDb.QueueLevel(6);
        hard.Difficulty = Difficulty.Hard;
        await TestDb.AddLevelsAsync(_db, hard);

        var byDifficulty = await CreateService().GetLevelsAsync(new LevelQuery { Difficulty = Difficulty.Hard }, TestDb.PlayerId, false);
        var locked = await CreateService().GetLevelsAsync(new LevelQuery { Status = LevelStatusFilter.Locked }, TestDb.PlayerId, false);

        Assert.Equal([6], byDifficulty.Items.Select(i => i.Number));
        Assert.Equal(5, locked.TotalCount); // levels 2..6
    }

    [Fact]
    public async Task GetPlayInfo_LockedLevel_Throws()
    {
        await SeedAsync(3);

        await Assert.ThrowsAsync<LevelLockedException>(() => CreateService().GetPlayInfoAsync(2, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task GetPlayInfo_ReturnsNeighbours()
    {
        await SeedAsync(3);

        var info = await CreateService().GetPlayInfoAsync(2, TestDb.PlayerId, isAdmin: true);

        Assert.Equal(1, info.PreviousLevelNumber);
        Assert.Equal(3, info.NextLevelNumber);
    }

    [Fact]
    public async Task GetPlayInfo_MissingLevel_Throws404()
    {
        await SeedAsync(1);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService().GetPlayInfoAsync(404, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task GetBoard_ReturnsArrows()
    {
        await SeedAsync(1);
        var level = _db.Levels.Single();

        var board = await CreateService().GetBoardAsync(level.Id, TestDb.PlayerId, false);

        Assert.Equal(3, board.Arrows.Count);
        Assert.All(board.Arrows, a => Assert.Equal(Direction.Right, a.Direction));
    }

    [Fact]
    public async Task ProgressSummary_PointsToNextLevel()
    {
        await SeedAsync(4);
        var levels = _db.Levels.OrderBy(l => l.Number).ToList();
        await TestDb.CompleteAsync(_db, TestDb.PlayerId, levels[0], mistakes: 1);
        await TestDb.CompleteAsync(_db, TestDb.PlayerId, levels[1]);

        var summary = await CreateService().GetProgressSummaryAsync(TestDb.PlayerId);

        Assert.Equal(2, summary.CompletedLevels);
        Assert.Equal(4, summary.TotalLevels);
        Assert.Equal(5, summary.TotalStars);
        Assert.Equal(3, summary.NextLevelNumber);
        Assert.Equal(50, summary.PercentComplete);
    }

    private async Task SeedAsync(int count)
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        await TestDb.AddLevelsAsync(_db, Enumerable.Range(1, count).Select(n => TestDb.QueueLevel(n)).ToArray());
    }
}
