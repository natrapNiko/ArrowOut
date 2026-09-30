using ArrowOut.Data;
using ArrowOut.Data.Common;
using ArrowOut.Game.Generation;
using ArrowOut.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Services.Leaderboard;

public interface ILeaderboardService
{
    // One ranking per kind. Only wins of that kind count.
    Task<PagedResult<LeaderboardEntry>> GetPageAsync(ChallengeKind kind, int page, int pageSize, CancellationToken cancellationToken = default);
}

// Separate rankings for Easy, Medium and Hard. Sorted by points on that kind, then number of
// wins, then stars, then fewest mistakes. It's all worked out in SQL.
// Admins are left out, since their games are just testing.
public sealed class LeaderboardService(ApplicationDbContext dbContext) : ILeaderboardService
{
    public async Task<PagedResult<LeaderboardEntry>> GetPageAsync(ChallengeKind kind, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var adminIds =
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == Roles.Administrator
            select userRole.UserId;

        var won = dbContext.Challenges
            .AsNoTracking()
            .Where(c => c.IsCompleted && c.Kind == kind && !adminIds.Contains(c.OwnerId));

        var total = await won.Select(c => c.OwnerId).Distinct().CountAsync(cancellationToken);
        var size = PagedResult<LeaderboardEntry>.NormalizePageSize(pageSize);
        var current = PagedResult<LeaderboardEntry>.ClampPage(page, size, total);

        var rows = await won
            .GroupBy(c => new { c.OwnerId, c.Owner.DisplayName, c.Owner.UserName })
            .Select(g => new
            {
                g.Key.DisplayName,
                g.Key.UserName,
                Points = g.Sum(c => c.Points),
                Completed = g.Count(),
                Stars = g.Sum(c => c.Stars),
                Mistakes = g.Sum(c => c.BestMistakes ?? 0),
            })
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Completed)
            .ThenByDescending(r => r.Stars)
            .ThenBy(r => r.Mistakes)
            .ThenBy(r => r.UserName)
            .Skip((current - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var entries = rows
            .Select((r, i) => new LeaderboardEntry(
                ((current - 1) * size) + i + 1,
                PublicName(r.DisplayName, r.UserName),
                r.Points,
                r.Completed,
                r.Stars,
                r.Mistakes))
            .ToList();

        return new PagedResult<LeaderboardEntry>(entries, current, size, total);
    }

    // Never show e-mails publicly. Without a display name you get the first two letters and ***.
    internal static string PublicName(string? displayName, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        var local = userName?.Split('@')[0] ?? string.Empty;
        return local.Length <= 2 ? "Player" : $"{local[..2]}***";
    }
}
