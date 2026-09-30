using ArrowOut.Game;
using ArrowOut.Game.Replay;
using ArrowOut.Game.Solving;

namespace ArrowOut.Tests.Game;

public class SolverAndHintTests
{
    private readonly GreedySolver _solver = new();
    private readonly UnblockingHintProvider _hints = new();

    private static ArrowPiece Arrow(int id, int x, int y, Direction direction, int length = 1) =>
        new(id, new GridPoint(x, y), direction, length);

    // Three arrows in a row pointing right: they have to go front to back (3, 2, 1).
    private static Board Queue() => Board.Create(5, 3,
    [
        Arrow(1, 1, 1, Direction.Right),
        Arrow(2, 2, 1, Direction.Right),
        Arrow(3, 3, 1, Direction.Right),
    ]);

    private static Board HeadOn() => Board.Create(4, 3,
    [
        Arrow(1, 0, 1, Direction.Right),
        Arrow(2, 3, 1, Direction.Left),
    ]);

    [Fact]
    public void Solve_FindsValidOrder()
    {
        var result = _solver.Solve(Queue());

        Assert.True(result.IsSolvable);
        Assert.Equal([3, 2, 1], result.Order);
        Assert.Empty(result.StuckArrowIds);
    }

    [Fact]
    public void Solve_DetectsHeadOnDeadlock()
    {
        var result = _solver.Solve(HeadOn());

        Assert.False(result.IsSolvable);
        Assert.Equal([1, 2], result.StuckArrowIds.Order());
    }

    [Fact]
    public void Solve_DetectsCyclicJamOfFour()
    {
        // A pinwheel: every arrow runs into the next one.
        var board = Board.Create(4, 4,
        [
            Arrow(1, 0, 0, Direction.Right),
            Arrow(2, 3, 0, Direction.Down),
            Arrow(3, 3, 3, Direction.Left),
            Arrow(4, 0, 3, Direction.Up),
        ]);

        Assert.False(_solver.Solve(board).IsSolvable);
    }

    [Fact]
    public void Solve_DoesNotMutateInput()
    {
        var board = Queue();

        _solver.Solve(board);

        Assert.Equal(3, board.Count);
    }

    [Fact]
    public void SolveOrder_ReplaysAsAWin()
    {
        var board = Queue();
        var order = _solver.Solve(board).Order;

        Assert.True(GameReplayer.Replay(board, order, maxLives: 1).IsWin);
    }

    [Fact]
    public void Hint_ReturnsAFreeArrow()
    {
        var board = Queue();

        var hint = _hints.GetHint(board);

        Assert.Equal(3, hint);
        Assert.True(board.IsFree(hint!.Value));
    }

    [Fact]
    public void Hint_ReturnsNullWhenDeadlocked()
    {
        Assert.Null(_hints.GetHint(HeadOn()));
    }

    [Fact]
    public void Hint_PrefersTheArrowThatUnblocksMost()
    {
        // #1 is free but doesn't help anyone. #2 is free and unblocks #3.
        var board = Board.Create(5, 5,
        [
            Arrow(1, 0, 0, Direction.Up),
            Arrow(2, 4, 2, Direction.Right),
            Arrow(3, 2, 2, Direction.Right),
        ]);

        Assert.Equal(2, _hints.GetHint(board));
    }

    [Fact]
    public void Analyzer_MeasuresDepth()
    {
        var metrics = PuzzleAnalyzer.Analyze(Queue());

        Assert.True(metrics.IsSolvable);
        Assert.Equal(3, metrics.Depth);
        Assert.Equal(1, metrics.InitiallyFree);
    }

    [Fact]
    public void Analyzer_ReportsUnsolvable()
    {
        var metrics = PuzzleAnalyzer.Analyze(HeadOn());

        Assert.False(metrics.IsSolvable);
        Assert.Equal(0, metrics.Score);
        Assert.Equal(Difficulty.Easy, PuzzleAnalyzer.Classify(metrics));
    }
}
