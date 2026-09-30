namespace ArrowOut.Game.Solving;

public sealed class SolveResult
{
    private SolveResult(bool isSolvable, IReadOnlyList<int> order, IReadOnlyList<int> stuckArrowIds)
    {
        IsSolvable = isSolvable;
        Order = order;
        StuckArrowIds = stuckArrowIds;
    }

    public bool IsSolvable { get; }

    // An order to remove the arrows in. Covers all of them if solvable, otherwise as far as it got.
    public IReadOnlyList<int> Order { get; }

    // Arrows that can never get out. Empty if the board is solvable.
    public IReadOnlyList<int> StuckArrowIds { get; }

    public static SolveResult Solved(IReadOnlyList<int> order) => new(true, order, []);

    public static SolveResult Deadlocked(IReadOnlyList<int> partialOrder, IReadOnlyList<int> stuck) =>
        new(false, partialOrder, stuck);
}
