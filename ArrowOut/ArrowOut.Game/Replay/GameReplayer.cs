namespace ArrowOut.Game.Replay;

// Replays the taps the browser sent on the real board. We never just believe the browser
// saying "I won". Only what this replay shows counts.
public static class GameReplayer
{
    public const int MaxTaps = 2_000;

    public static ReplayResult Replay(Board initialBoard, IReadOnlyList<int> taps, int maxLives)
    {
        ArgumentNullException.ThrowIfNull(initialBoard);
        ArgumentNullException.ThrowIfNull(taps);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLives, 1);

        if (taps.Count > MaxTaps)
        {
            return ReplayResult.Invalid(MaxTaps, $"A replay may contain at most {MaxTaps} taps.");
        }

        var board = initialBoard.Clone();
        var mistakes = 0;

        for (var i = 0; i < taps.Count; i++)
        {
            if (board.IsCleared)
            {
                return ReplayResult.Invalid(i, "Taps were recorded after the board was cleared.");
            }

            if (mistakes >= maxLives)
            {
                return ReplayResult.Invalid(i, "Taps were recorded after all lives were lost.");
            }

            var arrowId = taps[i];
            if (!board.Contains(arrowId))
            {
                return ReplayResult.Invalid(i, $"Arrow #{arrowId} is not on the board at tap {i + 1}.");
            }

            if (!board.Move(arrowId).IsSuccess)
            {
                mistakes++;
            }
        }

        return ReplayResult.Completed(board.IsCleared, mistakes, mistakes >= maxLives, board.Count);
    }
}
