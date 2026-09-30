namespace ArrowOut.Game;

// An arrow on the board. It's a chain of neighbouring cells, head first, and it can bend like
// a snake. The head always points the same way as the last bit of the body.
// When you tap it, it moves like a train: the head goes straight and the body follows
// behind it. So only the cells in front of the head matter for whether it gets out.
public sealed class ArrowPiece : IEquatable<ArrowPiece>
{
    public const int MinLength = 1;
    public const int MaxLength = 64;

    private readonly GridPoint[] _cells;

    // Straight arrow: the body is the length - 1 cells behind the head.
    public ArrowPiece(int id, GridPoint head, Direction direction, int length)
        : this(id, StraightCells(head, direction, length), direction)
    {
    }

    // Bent arrow from a list of cells, head first.
    public ArrowPiece(int id, IReadOnlyList<GridPoint> cells, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (!direction.IsDefinedDirection())
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
        }

        if (cells.Count is < MinLength or > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cells), cells.Count, $"Arrow length must be between {MinLength} and {MaxLength}.");
        }

        var seen = new HashSet<GridPoint>();
        for (var i = 0; i < cells.Count; i++)
        {
            if (!seen.Add(cells[i]))
            {
                throw new ArgumentException($"Arrow path visits {cells[i]} twice.", nameof(cells));
            }

            if (i > 0 && !AreAdjacent(cells[i - 1], cells[i]))
            {
                throw new ArgumentException($"Arrow path is not continuous at {cells[i]}.", nameof(cells));
            }
        }

        if (cells.Count > 1 && cells[1] != cells[0].Step(direction.Opposite()))
        {
            throw new ArgumentException("The arrow head must point along its last body segment.", nameof(cells));
        }

        Id = id;
        Direction = direction;
        _cells = [.. cells];
    }

    public int Id { get; }

    public GridPoint Head => _cells[0];

    public Direction Direction { get; }

    public int Length => _cells.Length;

    public GridPoint Tail => _cells[^1];

    // Head first.
    public IReadOnlyList<GridPoint> Cells => _cells;

    public bool IsStraight
    {
        get
        {
            var back = Direction.Opposite();
            for (var i = 1; i < _cells.Length; i++)
            {
                if (_cells[i] != _cells[i - 1].Step(back))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public ArrowPiece WithId(int id) => new(id, _cells, Direction);

    public bool Equals(ArrowPiece? other) =>
        other is not null
        && Id == other.Id
        && Direction == other.Direction
        && _cells.AsSpan().SequenceEqual(other._cells);

    public override bool Equals(object? obj) => Equals(obj as ArrowPiece);

    public override int GetHashCode() => HashCode.Combine(Id, Head, Direction, Length);

    public override string ToString() => $"#{Id} {Direction} len {Length} at {Head}";

    private static bool AreAdjacent(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;

    private static GridPoint[] StraightCells(GridPoint head, Direction direction, int length)
    {
        if (!direction.IsDefinedDirection())
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
        }

        if (length is < MinLength or > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, $"Arrow length must be between {MinLength} and {MaxLength}.");
        }

        var back = direction.Opposite();
        var cells = new GridPoint[length];
        for (var i = 0; i < length; i++)
        {
            cells[i] = head.Step(back, i);
        }

        return cells;
    }
}
