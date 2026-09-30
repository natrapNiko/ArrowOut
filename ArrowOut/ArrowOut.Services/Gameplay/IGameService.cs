using ArrowOut.Services.Models;

namespace ArrowOut.Services.Gameplay;

// Game actions for admin levels, used by the API. Every call checks the player can open the level.
public interface IGameService
{
    Task StartAttemptAsync(int levelId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<HintResult> GetHintAsync(int levelId, IEnumerable<int> remainingArrowIds, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<MoveCheckResult> CheckMoveAsync(int levelId, IEnumerable<int> remainingArrowIds, int arrowId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    // Replays the taps on the server. Only a real win gets saved.
    Task<CompletionResult> SubmitCompletionAsync(int levelId, CompletionRequest request, string userId, bool isAdmin, CancellationToken cancellationToken = default);
}
