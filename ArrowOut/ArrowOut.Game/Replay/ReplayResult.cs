namespace ArrowOut.Game.Replay;

public sealed class ReplayResult
{
    private ReplayResult(bool isValid, bool isCleared, int mistakes, bool livesExhausted, int remaining,
        int? invalidTapIndex, string? error)
    {
        IsValid = isValid;
        IsCleared = isCleared;
        Mistakes = mistakes;
        LivesExhausted = livesExhausted;
        RemainingArrows = remaining;
        InvalidTapIndex = invalidTapIndex;
        Error = error;
    }

    // False if the taps don't make sense (someone messed with them, or they got corrupted).
    public bool IsValid { get; }

    public bool IsCleared { get; }

    public int Mistakes { get; }

    public bool LivesExhausted { get; }

    public int RemainingArrows { get; }

    public int? InvalidTapIndex { get; }

    public string? Error { get; }

    // A real win: every arrow is out and there's at least one heart left.
    public bool IsWin => IsValid && IsCleared && !LivesExhausted;

    internal static ReplayResult Completed(bool isCleared, int mistakes, bool livesExhausted, int remaining) =>
        new(true, isCleared, mistakes, livesExhausted, remaining, null, null);

    internal static ReplayResult Invalid(int tapIndex, string error) =>
        new(false, false, 0, false, 0, tapIndex, error);
}
