namespace ArrowOut.Game.Solving;

public sealed record PuzzleMetrics(int ArrowCount, int InitiallyFree, int Depth, bool IsSolvable)
{
    // Rough 0 to 100 score for how hard the board is.
    public int Score => !IsSolvable
        ? 0
        : Math.Clamp((ArrowCount * 2) + (Depth * 6) - (InitiallyFree * 2), 0, 100);
}

// Works out some numbers about a board, used to guess how hard it is.
public static class PuzzleAnalyzer
{
    // Depth = how many rounds it takes if you remove every free arrow at once each round.
    // A deep board makes you think several steps ahead, a shallow one you can almost tap randomly.
    public static PuzzleMetrics Analyze(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var working = board.Clone();
        var initiallyFree = working.FreeArrowIds().Count();
        var depth = 0;

        while (!working.IsCleared)
        {
            var free = working.FreeArrowIds().ToList();
            if (free.Count == 0)
            {
                return new PuzzleMetrics(board.Count, initiallyFree, depth, IsSolvable: false);
            }

            foreach (var id in free)
            {
                working.Move(id);
            }

            depth++;
        }

        return new PuzzleMetrics(board.Count, initiallyFree, depth, IsSolvable: true);
    }

    public static Difficulty Classify(PuzzleMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.Score switch
        {
            < 25 => Difficulty.Easy,
            < 50 => Difficulty.Medium,
            < 75 => Difficulty.Hard,
            _ => Difficulty.Expert,
        };
    }
}
