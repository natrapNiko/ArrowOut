using ArrowOut.Data;
using ArrowOut.Data.Common;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Services.Levels;

public sealed class LevelService(ApplicationDbContext dbContext, ILevelAccessGuard accessGuard) : ILevelService
{
    public async Task<PagedResult<LevelListItem>> GetLevelsAsync(
        LevelQuery query, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var statuses = await accessGuard.GetStatusesAsync(userId, isAdmin, cancellationToken);
        var stars = await dbContext.PlayerProgress
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.LevelId, p => p.Stars, cancellationToken);

        var levels = dbContext.Levels.AsNoTracking().Where(l => l.IsPublished);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // EF turns this into a parameterised query, so the search text never ends up in the SQL itself.
            levels = int.TryParse(search, out var number)
                ? levels.Where(l => l.Number == number || l.Name.Contains(search))
                : levels.Where(l => l.Name.Contains(search));
        }

        if (query.Difficulty is { } difficulty)
        {
            levels = levels.Where(l => l.Difficulty == difficulty);
        }

        // The status depends on the player's progress, so it's worked out in memory from a small query.
        // Even with thousands of levels that's only a few KB. If it ever gets slow, do it in SQL instead.
        var rows = await levels
            .OrderBy(l => l.Number)
            .Select(l => new
            {
                l.Id,
                l.Number,
                l.Name,
                l.Difficulty,
                l.Width,
                l.Height,
                ArrowCount = l.Arrows.Count,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new LevelListItem(
                r.Id,
                r.Number,
                r.Name,
                r.Difficulty,
                r.Width,
                r.Height,
                r.ArrowCount,
                statuses.TryGetValue(r.Id, out var status) ? status : LevelStatus.Locked,
                stars.TryGetValue(r.Id, out var s) ? s : 0))
            .Where(i => MatchesStatus(i.Status, query.Status))
            .ToList();

        var pageSize = PagedResult<LevelListItem>.NormalizePageSize(query.PageSize);
        var page = PagedResult<LevelListItem>.ClampPage(query.Page, pageSize, items.Count);
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<LevelListItem>(pageItems, page, pageSize, items.Count);
    }

    public async Task<LevelPlayInfo> GetPlayInfoAsync(
        int levelNumber, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var level = await dbContext.Levels
            .AsNoTracking()
            .Where(l => l.Number == levelNumber)
            .Select(l => new { l.Id, l.Number, l.Name, l.Difficulty, l.MaxLives, l.IsPublished })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Level", levelNumber);

        var status = await accessGuard.EnsureCanPlayAsync(
            level.Id, level.Number, level.IsPublished, userId, isAdmin, cancellationToken);

        var bestStars = await dbContext.PlayerProgress
            .Where(p => p.UserId == userId && p.LevelId == level.Id)
            .Select(p => p.Stars)
            .FirstOrDefaultAsync(cancellationToken);

        var previous = await dbContext.Levels
            .Where(l => l.IsPublished && l.Number < level.Number)
            .OrderByDescending(l => l.Number)
            .Select(l => (int?)l.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var next = await GetNextPublishedNumberAsync(level.Number, cancellationToken);

        return new LevelPlayInfo(
            level.Id, level.Number, level.Name, level.Difficulty, level.MaxLives, status, bestStars, previous, next);
    }

    public async Task<LevelBoardDto> GetBoardAsync(
        int levelId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var level = await dbContext.Levels
            .AsNoTracking()
            .Include(l => l.Arrows)
            .FirstOrDefaultAsync(l => l.Id == levelId, cancellationToken)
            ?? throw new EntityNotFoundException("Level", levelId);

        await accessGuard.EnsureCanPlayAsync(level.Id, level.Number, level.IsPublished, userId, isAdmin, cancellationToken);

        var arrows = level.Arrows
            .OrderBy(a => a.Id)
            .Select(a => ArrowDto.FromPiece(a.ToPiece()))
            .ToList();

        return new LevelBoardDto(
            level.Id, level.Number, level.Name, level.Width, level.Height, level.MaxLives, level.Difficulty, arrows);
    }

    public async Task<ProgressSummary> GetProgressSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var total = await dbContext.Levels.CountAsync(l => l.IsPublished, cancellationToken);

        var progress = await dbContext.PlayerProgress
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsCompleted && p.Level.IsPublished)
            .Select(p => new { p.Stars })
            .ToListAsync(cancellationToken);

        var statuses = await accessGuard.GetStatusesAsync(userId, isAdmin: false, cancellationToken);
        var unlockedIds = statuses
            .Where(s => s.Value == LevelStatus.Unlocked)
            .Select(s => s.Key)
            .ToList();

        int? nextNumber = unlockedIds.Count == 0
            ? null
            : await dbContext.Levels
                .Where(l => unlockedIds.Contains(l.Id))
                .MinAsync(l => (int?)l.Number, cancellationToken);

        return new ProgressSummary(
            progress.Count,
            total,
            progress.Sum(p => p.Stars),
            total * DataConstants.Progress.StarsMax,
            nextNumber);
    }

    internal Task<int?> GetNextPublishedNumberAsync(int afterNumber, CancellationToken cancellationToken) =>
        dbContext.Levels
            .Where(l => l.IsPublished && l.Number > afterNumber)
            .OrderBy(l => l.Number)
            .Select(l => (int?)l.Number)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool MatchesStatus(LevelStatus status, LevelStatusFilter filter) => filter switch
    {
        LevelStatusFilter.All => true,
        LevelStatusFilter.Completed => status == LevelStatus.Completed,
        LevelStatusFilter.Unlocked => status == LevelStatus.Unlocked,
        LevelStatusFilter.Locked => status == LevelStatus.Locked,
        _ => true,
    };
}
