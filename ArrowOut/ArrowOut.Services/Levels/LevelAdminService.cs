using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ArrowOut.Data;
using ArrowOut.Data.Models;
using ArrowOut.Game;
using ArrowOut.Game.Generation;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Services.Levels;

public sealed class LevelAdminService(
    ApplicationDbContext dbContext,
    ILevelDesignValidator designValidator) : ILevelAdminService
{
    public async Task<PagedResult<AdminLevelListItem>> GetPagedAsync(AdminLevelQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var levels = dbContext.Levels.AsNoTracking();

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            levels = int.TryParse(search, out var number)
                ? levels.Where(l => l.Number == number || l.Name.Contains(search))
                : levels.Where(l => l.Name.Contains(search));
        }

        if (query.Difficulty is { } difficulty)
        {
            levels = levels.Where(l => l.Difficulty == difficulty);
        }

        if (query.IsPublished is { } published)
        {
            levels = levels.Where(l => l.IsPublished == published);
        }

        levels = query.Sort switch
        {
            AdminLevelSort.NumberDesc => levels.OrderByDescending(l => l.Number),
            AdminLevelSort.NameAsc => levels.OrderBy(l => l.Name).ThenBy(l => l.Number),
            AdminLevelSort.Newest => levels.OrderByDescending(l => l.CreatedOn).ThenBy(l => l.Number),
            _ => levels.OrderBy(l => l.Number),
        };

        var total = await levels.CountAsync(cancellationToken);
        var pageSize = PagedResult<AdminLevelListItem>.NormalizePageSize(query.PageSize);
        var page = PagedResult<AdminLevelListItem>.ClampPage(query.Page, pageSize, total);

        var items = await levels
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AdminLevelListItem(
                l.Id,
                l.Number,
                l.Name,
                l.Difficulty,
                l.Width,
                l.Height,
                l.Arrows.Count,
                l.IsPublished,
                l.Progress.Count(p => p.IsCompleted),
                l.CreatedOn))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminLevelListItem>(items, page, pageSize, total);
    }

    public async Task<LevelInputModel> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var level = await dbContext.Levels
            .AsNoTracking()
            .Include(l => l.Arrows)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Level", id);

        return new LevelInputModel
        {
            Number = level.Number,
            Name = level.Name,
            Width = level.Width,
            Height = level.Height,
            MaxLives = level.MaxLives,
            Difficulty = level.Difficulty,
            IsPublished = level.IsPublished,
            ArrowsJson = SerializeArrows(level.Arrows.OrderBy(a => a.Id)),
        };
    }

    public async Task<LevelInputModel> CreateDraftAsync(CancellationToken cancellationToken = default)
    {
        var maxNumber = await dbContext.Levels.MaxAsync(l => (int?)l.Number, cancellationToken) ?? 0;
        return new LevelInputModel { Number = maxNumber + 1, Name = $"Level {maxNumber + 1}" };
    }

    public async Task<int> CreateAsync(LevelInputModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var arrows = ValidateDesign(model.Width, model.Height, model.ArrowsJson);
        await EnsureNumberAvailableAsync(model.Number, null, cancellationToken);

        var level = new Level();
        Apply(level, model, arrows);
        dbContext.Levels.Add(level);
        await SaveAsync(model.Number, cancellationToken);

        return level.Id;
    }

    public async Task UpdateAsync(int id, LevelInputModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var level = await dbContext.Levels
            .Include(l => l.Arrows)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Level", id);

        var arrows = ValidateDesign(model.Width, model.Height, model.ArrowsJson);
        await EnsureNumberAvailableAsync(model.Number, id, cancellationToken);

        var layoutChanged = HasLayoutChanged(level, model, arrows);

        // Just replace all the arrows. The old ids are gone after this, so if someone still has the
        // old board open, the replay check rejects it. Progress stays if only the name etc. changed.
        dbContext.Arrows.RemoveRange(level.Arrows.ToList()); // ToList() because EF changes the collection while it removes them
        Apply(level, model, arrows);

        if (layoutChanged)
        {
            // The old results were for a different puzzle. They're deleted in the same SaveChanges.
            var staleProgress = await dbContext.PlayerProgress.Where(p => p.LevelId == id).ToListAsync(cancellationToken);
            dbContext.PlayerProgress.RemoveRange(staleProgress);
        }

        await SaveAsync(model.Number, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var level = await dbContext.Levels.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Level", id);

        dbContext.Levels.Remove(level);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPublishedAsync(int id, bool isPublished, CancellationToken cancellationToken = default)
    {
        var level = await dbContext.Levels.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Level", id);

        level.IsPublished = isPublished;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsNumberAvailableAsync(int number, int? excludeLevelId, CancellationToken cancellationToken = default) =>
        !await dbContext.Levels.AnyAsync(
            l => l.Number == number && (excludeLevelId == null || l.Id != excludeLevelId),
            cancellationToken);

    public LevelDesignReport Analyze(int width, int height, string? arrowsJson)
    {
        var (arrows, errors) = designValidator.ParseArrows(arrowsJson);
        return errors.Count > 0
            ? new LevelDesignReport(errors, false, arrows.Count, 0, Difficulty.Easy)
            : designValidator.Validate(width, height, arrows);
    }

    public IReadOnlyList<ArrowInputModel> Generate(GenerateLevelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new GeneratorSettings
        {
            Width = request.Width,
            Height = request.Height,
            TargetFill = request.Fill,
            MinLength = 2,
            MaxLength = request.MaxLength,
        };

        return new LevelGenerator(Random.Shared)
            .Generate(settings)
            .Select(ArrowInputModel.FromPiece)
            .ToList();
    }

    public async Task<(string FileName, byte[] Content)> ExportAsync(int id, CancellationToken cancellationToken = default)
    {
        var level = await dbContext.Levels
            .AsNoTracking()
            .Include(l => l.Arrows)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Level", id);

        var file = new LevelFile
        {
            Number = level.Number,
            Name = level.Name,
            Width = level.Width,
            Height = level.Height,
            MaxLives = level.MaxLives,
            Difficulty = level.Difficulty,
            Arrows = level.Arrows.OrderBy(a => a.Id).Select(a => ArrowInputModel.FromPiece(a.ToPiece())).ToList(),
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, LevelFile.JsonOptions);
        return ($"arrowout-level-{level.Number:D4}.json", bytes);
    }

    public async Task<int> ImportAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        LevelFile? file;
        try
        {
            file = await JsonSerializer.DeserializeAsync<LevelFile>(content, LevelFile.JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw new InvalidLevelDesignException(["The file is not a valid level JSON document."]);
        }

        if (file is null || file.Format != LevelFile.FormatName || file.Version != LevelFile.CurrentVersion)
        {
            throw new InvalidLevelDesignException([$"Expected a '{LevelFile.FormatName}' v{LevelFile.CurrentVersion} file."]);
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(file, new ValidationContext(file), results, validateAllProperties: true))
        {
            throw new InvalidLevelDesignException(results.Select(r => r.ErrorMessage ?? "Invalid value.").ToList());
        }

        var model = new LevelInputModel
        {
            Number = file.Number,
            Name = file.Name,
            Width = file.Width,
            Height = file.Height,
            MaxLives = file.MaxLives,
            Difficulty = file.Difficulty,
            IsPublished = false, // imports always come in as drafts so an admin can check them first
            ArrowsJson = JsonSerializer.Serialize(file.Arrows, LevelDesignValidator.JsonOptions),
        };

        return await CreateAsync(model, cancellationToken);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default) =>
        new(
            await dbContext.Levels.CountAsync(cancellationToken),
            await dbContext.Levels.CountAsync(l => l.IsPublished, cancellationToken),
            await dbContext.Users.CountAsync(cancellationToken),
            await dbContext.PlayerProgress.SumAsync(p => p.Completions, cancellationToken),
            await dbContext.PlayerProgress.SumAsync(p => p.Attempts, cancellationToken));

    private IReadOnlyList<ArrowInputModel> ValidateDesign(int width, int height, string arrowsJson)
    {
        var (arrows, parseErrors) = designValidator.ParseArrows(arrowsJson);
        if (parseErrors.Count > 0)
        {
            throw new InvalidLevelDesignException(parseErrors);
        }

        var report = designValidator.Validate(width, height, arrows);
        return report.IsValid ? arrows : throw new InvalidLevelDesignException(report.Errors);
    }

    private async Task EnsureNumberAvailableAsync(int number, int? excludeId, CancellationToken cancellationToken)
    {
        if (!await IsNumberAvailableAsync(number, excludeId, cancellationToken))
        {
            throw new DuplicateLevelNumberException(number);
        }
    }

    private async Task SaveAsync(int number, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Levels_Number", StringComparison.Ordinal) == true)
        {
            // Someone took the number between our check and the insert. The unique index catches that.
            throw new DuplicateLevelNumberException(number);
        }
    }

    private static void Apply(Level level, LevelInputModel model, IReadOnlyList<ArrowInputModel> arrows)
    {
        level.Number = model.Number;
        level.Name = model.Name.Trim();
        level.Width = model.Width;
        level.Height = model.Height;
        level.MaxLives = model.MaxLives;
        level.Difficulty = model.Difficulty;
        level.IsPublished = model.IsPublished;
        level.ReplaceArrows(arrows.Select((a, i) => Arrow.FromPiece(a.ToPiece(i + 1))));
    }

    private static bool HasLayoutChanged(Level level, LevelInputModel model, IReadOnlyList<ArrowInputModel> arrows)
    {
        if (level.Width != model.Width || level.Height != model.Height || level.Arrows.Count != arrows.Count)
        {
            return true;
        }

        var existing = level.Arrows.Select(a => (a.Direction, Arrow.EncodePath(a.ToPiece().Cells))).ToHashSet();
        return arrows.Any(a => !existing.Contains((a.Direction, Arrow.EncodePath(a.ToPiece(0).Cells))));
    }

    private static string SerializeArrows(IEnumerable<Arrow> arrows) =>
        JsonSerializer.Serialize(
            arrows.Select(a => ArrowInputModel.FromPiece(a.ToPiece())),
            LevelDesignValidator.JsonOptions);
}
