using ArrowOut.Services.Models;

namespace ArrowOut.Services.Levels;

// Level queries for players (level list, board, progress).
public interface ILevelService
{
    Task<PagedResult<LevelListItem>> GetLevelsAsync(LevelQuery query, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    // Throws EntityNotFoundException or LevelLockedException.
    Task<LevelPlayInfo> GetPlayInfoAsync(int levelNumber, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    // Throws EntityNotFoundException or LevelLockedException.
    Task<LevelBoardDto> GetBoardAsync(int levelId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<ProgressSummary> GetProgressSummaryAsync(string userId, CancellationToken cancellationToken = default);
}
