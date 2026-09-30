using ArrowOut.Data.Common;
using ArrowOut.Data.Models.Common;

namespace ArrowOut.Data.Models;

// A player's progress on one level. Only change it through the methods below so the
// counters and best scores stay in sync.
public class PlayerProgress : BaseAuditableModel<int>
{
    public PlayerProgress(string userId, int levelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        UserId = userId;
        LevelId = levelId;
    }

    // For EF Core.
    protected PlayerProgress()
    {
        UserId = string.Empty;
    }

    public string UserId { get; private set; }

    public virtual ApplicationUser User { get; private set; } = null!;

    public int LevelId { get; private set; }

    public virtual Level Level { get; private set; } = null!;

    public bool IsCompleted { get; private set; }

    public int Stars { get; private set; }

    public int? BestMistakes { get; private set; }

    public int Attempts { get; private set; }

    public int Completions { get; private set; }

    public DateTime? FirstCompletedOn { get; private set; }

    public DateTime LastPlayedOn { get; private set; }

    public static int CalculateStars(int mistakes) => mistakes switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(mistakes), mistakes, "Mistakes cannot be negative."),
        0 => DataConstants.Progress.StarsMax,
        1 => 2,
        _ => 1,
    };

    public void RecordAttempt(DateTime utcNow)
    {
        Attempts++;
        LastPlayedOn = utcNow;
    }

    // Saves a win. Returns true if it's a new personal best.
    public bool RecordWin(int mistakes, DateTime utcNow)
    {
        var stars = CalculateStars(mistakes);
        var isNewBest = BestMistakes is null || mistakes < BestMistakes;

        if (!IsCompleted)
        {
            IsCompleted = true;
            FirstCompletedOn = utcNow;
        }

        Completions++;
        LastPlayedOn = utcNow;

        if (isNewBest)
        {
            BestMistakes = mistakes;
        }

        Stars = Math.Max(Stars, stars);
        return isNewBest;
    }
}
