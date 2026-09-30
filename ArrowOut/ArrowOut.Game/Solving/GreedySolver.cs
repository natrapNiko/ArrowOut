namespace ArrowOut.Game.Solving;

// Solver that never needs to backtrack.
// Why that works: an arrow is free when every cell in front of it is empty. Removing an arrow
// only ever empties cells, so a free arrow stays free until the end of the game. That means
// it doesn't matter in which order you take the free arrows out, you always end up in the
// same place: either an empty board or a jam where nothing can move. So no searching needed.
// Cost is O(n^2 * s), where s is the side length.
public sealed class GreedySolver : IPuzzleSolver
{
    public SolveResult Solve(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var working = board.Clone();
        var order = new List<int>(working.Count);

        bool progressed;
        do
        {
            progressed = false;
            foreach (var id in working.FreeArrowIds().ToList())
            {
                // Removing something earlier in this pass can only free more arrows, never block one.
                working.Move(id);
                order.Add(id);
                progressed = true;
            }
        }
        while (progressed && !working.IsCleared);

        return working.IsCleared
            ? SolveResult.Solved(order)
            : SolveResult.Deadlocked(order, working.Arrows.Select(a => a.Id).ToList());
    }
}
