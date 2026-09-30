using ArrowOut.Data;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Services.Levels;

public interface ILevelAccessGuard
{
    // Status of every published level for this user.
    Task<IReadOnlyDictionary<int, LevelStatus>> GetStatusesAsync(string userId, bool isAdmin, CancellationToken cancellationToken = default);

    // Checks on the server that the player is allowed to play this level. Players can't see
    // unpublished levels at all, and locked ones are refused.
    Task<LevelStatus> EnsureCanPlayAsync(int levelId, int levelNumber, bool isPublished, string userId, bool isAdmin, CancellationToken cancellationToken = default);
}

public sealed class LevelAccessGuard(ApplicationDbContext dbContext) : ILevelAccessGuard
{
    public async Task<IReadOnlyDictionary<int, LevelStatus>> GetStatusesAsync(string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var published = await dbContext.Levels
            .AsNoTracking()
            .Where(l => l.IsPublished)
            .OrderBy(l => l.Number)
            .Select(l => new { l.Id, l.Number })
            .ToListAsync(cancellationToken);

        var completed = await dbContext.PlayerProgress
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsCompleted)
            .Select(p => p.LevelId)
            .ToListAsync(cancellationToken);

        return LevelAccessCalculator.Compute(
            published.Select(l => (l.Id, l.Number)).ToList(),
            completed.ToHashSet(),
            unlockAll: isAdmin);
    }

    public async Task<LevelStatus> EnsureCanPlayAsync(
        int levelId, int levelNumber, bool isPublished, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (!isPublished)
        {
            if (!isAdmin)
            {
                throw new EntityNotFoundException("Level", levelNumber);
            }

            // Admins can test drafts. Drafts never count towards unlocking.
            var completedDraft = await dbContext.PlayerProgress
                .AnyAsync(p => p.UserId == userId && p.LevelId == levelId && p.IsCompleted, cancellationToken);
            return completedDraft ? LevelStatus.Completed : LevelStatus.Unlocked;
        }

        var statuses = await GetStatusesAsync(userId, isAdmin, cancellationToken);
        var status = statuses.TryGetValue(levelId, out var s) ? s : LevelStatus.Locked;

        return status == LevelStatus.Locked
            ? throw new LevelLockedException(levelNumber)
            : status;
    }
}
