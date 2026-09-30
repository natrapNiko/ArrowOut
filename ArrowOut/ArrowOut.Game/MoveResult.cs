namespace ArrowOut.Game;

// What happened when an arrow was tapped. Check IsSuccess, or switch on the type if you
// need the details.
public abstract class MoveResult
{
    protected MoveResult(int arrowId, int distance)
    {
        ArrowId = arrowId;
        Distance = distance;
    }

    public int ArrowId { get; }

    // How far the head moved. For an exit that's until the whole arrow is off the board,
    // for a crash it's the number of free cells before it hit something.
    public int Distance { get; }

    public abstract bool IsSuccess { get; }
}

// The arrow got out and is gone from the board.
public sealed class ExitMoveResult(int arrowId, int distance) : MoveResult(arrowId, distance)
{
    public override bool IsSuccess => true;
}

// The arrow hit another one. The board stays as it was.
public sealed class CollisionMoveResult(int arrowId, int distance, int blockerArrowId, GridPoint impactCell)
    : MoveResult(arrowId, distance)
{
    public int BlockerArrowId { get; } = blockerArrowId;

    public GridPoint ImpactCell { get; } = impactCell;

    public override bool IsSuccess => false;
}
