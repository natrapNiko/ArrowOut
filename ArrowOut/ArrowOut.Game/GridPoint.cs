namespace ArrowOut.Game;

// A cell position. X is the column, Y is the row, (0,0) is the top-left.
public readonly record struct GridPoint(int X, int Y)
{
    public GridPoint Offset(GridPoint delta, int times = 1) => new(X + (delta.X * times), Y + (delta.Y * times));

    public GridPoint Step(Direction direction, int times = 1) => Offset(direction.ToDelta(), times);

    public override string ToString() => $"({X},{Y})";
}
