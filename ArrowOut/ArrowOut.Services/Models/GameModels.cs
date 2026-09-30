using System.ComponentModel.DataAnnotations;
using ArrowOut.Game.Replay;

namespace ArrowOut.Services.Models;

// The board as the browser sees it: ids of the arrows still on it.
public class BoardStateRequest
{
    public const int MaxArrowIds = Game.BoardValidator.MaxArrows;

    [Required]
    [MaxLength(MaxArrowIds)]
    public IList<int> RemainingArrowIds { get; set; } = [];
}

public sealed class MoveRequest : BoardStateRequest
{
    [Range(1, int.MaxValue)]
    public int ArrowId { get; set; }
}

public sealed class CompletionRequest
{
    // Every arrow the player tapped, in order, crashes included.
    [Required]
    [MinLength(1)]
    [MaxLength(GameReplayer.MaxTaps)]
    public IList<int> Taps { get; set; } = [];

    [Range(0, 100)]
    public int HintsUsed { get; set; }
}

public sealed record HintResult(int? ArrowId)
{
    public bool HasHint => ArrowId.HasValue;
}

public sealed record MoveCheckResult(int ArrowId, bool IsSuccess, int Distance, int? BlockerArrowId);

public sealed record CompletionResult(
    int LevelId,
    int Stars,
    int Mistakes,
    bool IsNewBest,
    bool IsFirstCompletion,
    int? NextLevelNumber);
