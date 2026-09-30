using ArrowOut.Game;

namespace ArrowOut.Tests.Game;

public class BoardTests
{
    private static ArrowPiece Arrow(int id, int x, int y, Direction direction, int length = 1) =>
        new(id, new GridPoint(x, y), direction, length);

    [Fact]
    public void ArrowPiece_BodyExtendsBehindTheHead()
    {
        var arrow = Arrow(1, 2, 2, Direction.Right, 3);

        Assert.Equal([new GridPoint(2, 2), new GridPoint(1, 2), new GridPoint(0, 2)], arrow.Cells);
        Assert.Equal(new GridPoint(0, 2), arrow.Tail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void ArrowPiece_RejectsInvalidLength(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Arrow(1, 0, 0, Direction.Up, length));
    }

    private static ArrowPiece Bent(int id, Direction direction, params (int X, int Y)[] headFirst) =>
        new(id, headFirst.Select(c => new GridPoint(c.X, c.Y)).ToList(), direction);

    [Fact]
    public void BentArrow_KeepsItsPathAndIsNotStraight()
    {
        var arrow = Bent(1, Direction.Up, (1, 1), (1, 2), (2, 2), (2, 3));

        Assert.Equal(4, arrow.Length);
        Assert.Equal(new GridPoint(2, 3), arrow.Tail);
        Assert.False(arrow.IsStraight);
        Assert.True(Arrow(2, 1, 1, Direction.Up, 3).IsStraight);
    }

    [Fact]
    public void BentArrow_RejectsGapsInThePath()
    {
        Assert.Throws<ArgumentException>(() => Bent(1, Direction.Up, (1, 1), (1, 2), (3, 2)));
    }

    [Fact]
    public void BentArrow_RejectsRevisitedCells()
    {
        Assert.Throws<ArgumentException>(() => Bent(1, Direction.Up, (1, 1), (1, 2), (2, 2), (2, 1), (1, 1)));
    }

    [Fact]
    public void BentArrow_HeadMustContinueTheLastSegment()
    {
        // The body comes from below, so the head has to point Up. Right isn't allowed.
        Assert.Throws<ArgumentException>(() => Bent(1, Direction.Right, (1, 1), (1, 2), (2, 2)));
    }

    [Fact]
    public void BentArrow_OnlyTheCellsAheadOfTheHeadMatter()
    {
        // The body winds around the board, but the column above the head is empty.
        var board = Board.Create(4, 4,
        [
            Bent(1, Direction.Up, (1, 2), (1, 3), (2, 3), (3, 3), (3, 2)),
            Arrow(2, 0, 0, Direction.Left),
        ]);

        var result = board.Move(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2 + 5, result.Distance); // 2 free cells + the 5-cell body
    }

    [Fact]
    public void BentArrow_BlockedByAnotherArrowInFront()
    {
        var board = Board.Create(4, 4,
        [
            Bent(1, Direction.Up, (1, 2), (1, 3), (2, 3)),
            Arrow(2, 1, 0, Direction.Left),
        ]);

        var collision = Assert.IsType<CollisionMoveResult>(board.Move(1));

        Assert.Equal(2, collision.BlockerArrowId);
        Assert.Equal(1, collision.Distance);
    }

    [Fact]
    public void BentArrow_CurlingInFrontOfItsOwnHead_IsAPermanentJam()
    {
        // The head at (1,2) points Up into (1,1), which is part of its own body.
        var board = Board.Create(4, 4, [Bent(1, Direction.Up, (1, 2), (1, 3), (2, 3), (2, 2), (2, 1), (1, 1))]);

        var collision = Assert.IsType<CollisionMoveResult>(board.Move(1));

        Assert.Equal(1, collision.BlockerArrowId);
        Assert.False(new ArrowOut.Game.Solving.GreedySolver().Solve(board).IsSolvable);
    }

    [Fact]
    public void ArrowPiece_RejectsUndefinedDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Arrow(1, 0, 0, (Direction)7));
    }

    [Fact]
    public void Create_RejectsArrowOutsideTheBoard()
    {
        var exception = Assert.Throws<InvalidBoardException>(() =>
            Board.Create(3, 3, [Arrow(1, 0, 0, Direction.Right, 2)]));

        Assert.Contains(exception.Errors, e => e.Contains("outside", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_RejectsOverlappingArrows()
    {
        var exception = Assert.Throws<InvalidBoardException>(() =>
            Board.Create(4, 4, [Arrow(1, 2, 1, Direction.Right, 2), Arrow(2, 1, 1, Direction.Up)]));

        Assert.Contains(exception.Errors, e => e.Contains("overlaps", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_RejectsDuplicateIds()
    {
        var exception = Assert.Throws<InvalidBoardException>(() =>
            Board.Create(4, 4, [Arrow(1, 0, 0, Direction.Up), Arrow(1, 3, 3, Direction.Down)]));

        Assert.Contains(exception.Errors, e => e.Contains("more than once", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 97)]
    public void Create_RejectsInvalidDimensions(int width, int height)
    {
        Assert.Throws<InvalidBoardException>(() => Board.Create(width, height, [Arrow(1, 0, 0, Direction.Up)]));
    }

    [Fact]
    public void Create_RejectsEmptyBoard()
    {
        Assert.Throws<InvalidBoardException>(() => Board.Create(4, 4, []));
    }

    [Fact]
    public void Move_FreeArrow_ExitsAndIsRemoved()
    {
        var board = Board.Create(5, 5, [Arrow(1, 2, 2, Direction.Up, 2)]);

        var result = board.Move(1);

        Assert.IsType<ExitMoveResult>(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Distance); // 2 free cells ahead + length 2 to fully leave
        Assert.True(board.IsCleared);
        Assert.Null(board.ArrowAt(new GridPoint(2, 2)));
    }

    [Fact]
    public void Move_BlockedArrow_CollidesAndBoardIsUnchanged()
    {
        var board = Board.Create(5, 3, [Arrow(1, 0, 1, Direction.Right), Arrow(2, 3, 1, Direction.Up)]);

        var result = board.Move(1);

        var collision = Assert.IsType<CollisionMoveResult>(result);
        Assert.False(collision.IsSuccess);
        Assert.Equal(2, collision.BlockerArrowId);
        Assert.Equal(2, collision.Distance);
        Assert.Equal(new GridPoint(3, 1), collision.ImpactCell);
        Assert.Equal(2, board.Count);
    }

    [Fact]
    public void Move_ArrowBehindAnotherBecomesFreeAfterTheBlockerLeaves()
    {
        var board = Board.Create(5, 3, [Arrow(1, 0, 1, Direction.Right), Arrow(2, 3, 1, Direction.Up)]);

        Assert.False(board.IsFree(1));
        Assert.True(board.Move(2).IsSuccess);
        Assert.True(board.IsFree(1));
    }

    [Fact]
    public void Peek_DoesNotMutate()
    {
        var board = Board.Create(4, 4, [Arrow(1, 1, 1, Direction.Left)]);

        board.Peek(1);

        Assert.True(board.Contains(1));
    }

    [Fact]
    public void Move_UnknownArrow_Throws()
    {
        var board = Board.Create(4, 4, [Arrow(1, 1, 1, Direction.Left)]);

        Assert.Throws<ArgumentException>(() => board.Move(42));
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var board = Board.Create(4, 4, [Arrow(1, 1, 1, Direction.Left)]);
        var clone = board.Clone();

        clone.Move(1);

        Assert.True(board.Contains(1));
        Assert.True(clone.IsCleared);
    }

    [Fact]
    public void WithOnly_KeepsRequestedArrows()
    {
        var board = Board.Create(4, 4, [Arrow(1, 0, 0, Direction.Up), Arrow(2, 3, 3, Direction.Down), Arrow(3, 1, 2, Direction.Left)]);

        var subset = board.WithOnly([1, 3]);

        Assert.Equal([1, 3], subset.Arrows.Select(a => a.Id));
    }

    [Fact]
    public void WithOnly_RejectsUnknownIds()
    {
        var board = Board.Create(4, 4, [Arrow(1, 0, 0, Direction.Up)]);

        Assert.Throws<ArgumentException>(() => board.WithOnly([1, 99]));
    }

    [Theory]
    [InlineData(Direction.Up, 0, -1)]
    [InlineData(Direction.Right, 1, 0)]
    [InlineData(Direction.Down, 0, 1)]
    [InlineData(Direction.Left, -1, 0)]
    public void Direction_DeltaAndOppositeAreConsistent(Direction direction, int dx, int dy)
    {
        Assert.Equal(new GridPoint(dx, dy), direction.ToDelta());
        Assert.Equal(new GridPoint(-dx, -dy), direction.Opposite().ToDelta());
    }
}
