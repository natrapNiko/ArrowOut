namespace ArrowOut.Game;

// The board while you play. The grid is private, so the only way to change it is Move,
// which makes sure arrows never overlap or leave the grid.
public sealed class Board
{
    private readonly int?[,] _occupancy;
    private readonly SortedDictionary<int, ArrowPiece> _arrows;

    private Board(int width, int height, IEnumerable<ArrowPiece> arrows)
    {
        Width = width;
        Height = height;
        _occupancy = new int?[width, height];
        _arrows = [];

        foreach (var arrow in arrows)
        {
            Place(arrow);
        }
    }

    public int Width { get; }

    public int Height { get; }

    public int Count => _arrows.Count;

    public bool IsCleared => _arrows.Count == 0;

    // Sorted by id so the order is always the same.
    public IReadOnlyCollection<ArrowPiece> Arrows => _arrows.Values;

    // Throws InvalidBoardException if the layout is broken.
    public static Board Create(int width, int height, IEnumerable<ArrowPiece> arrows)
    {
        ArgumentNullException.ThrowIfNull(arrows);

        var list = arrows as IReadOnlyCollection<ArrowPiece> ?? arrows.ToList();
        var errors = BoardValidator.Validate(width, height, list);
        if (errors.Count > 0)
        {
            throw new InvalidBoardException(errors);
        }

        return new Board(width, height, list);
    }

    public bool Contains(int arrowId) => _arrows.ContainsKey(arrowId);

    public ArrowPiece GetArrow(int arrowId) =>
        _arrows.TryGetValue(arrowId, out var arrow)
            ? arrow
            : throw new ArgumentException($"Arrow #{arrowId} is not on the board.", nameof(arrowId));

    public int? ArrowAt(GridPoint cell) =>
        BoardValidator.IsInside(Width, Height, cell) ? _occupancy[cell.X, cell.Y] : null;

    // Can this arrow get out right now?
    public bool IsFree(int arrowId) => Peek(arrowId).IsSuccess;

    // What would happen if you tapped this arrow? Doesn't change anything.
    public MoveResult Peek(int arrowId)
    {
        var arrow = GetArrow(arrowId);
        var freeCells = 0;
        var cell = arrow.Head.Step(arrow.Direction);

        while (BoardValidator.IsInside(Width, Height, cell))
        {
            var occupant = _occupancy[cell.X, cell.Y];
            if (occupant is { } blockerId)
            {
                return new CollisionMoveResult(arrow.Id, freeCells, blockerId, cell);
            }

            freeCells++;
            cell = cell.Step(arrow.Direction);
        }

        return new ExitMoveResult(arrow.Id, freeCells + arrow.Length);
    }

    // Taps an arrow. If it gets out it's removed, if it crashes nothing changes.
    public MoveResult Move(int arrowId)
    {
        var result = Peek(arrowId);
        if (result.IsSuccess)
        {
            Remove(arrowId);
        }

        return result;
    }

    public IEnumerable<int> FreeArrowIds() => _arrows.Keys.Where(IsFree);

    public Board Clone() => new(Width, Height, _arrows.Values);

    // Copy of the board with only these arrows left. The server uses it to rebuild what the
    // player sees. Ids that aren't on the board throw, so nobody can sneak in fake arrows.
    public Board WithOnly(IEnumerable<int> remainingArrowIds)
    {
        ArgumentNullException.ThrowIfNull(remainingArrowIds);

        var ids = remainingArrowIds.ToHashSet();
        var unknown = ids.Where(id => !_arrows.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Unknown arrow id(s): {string.Join(", ", unknown)}.", nameof(remainingArrowIds));
        }

        return new Board(Width, Height, _arrows.Values.Where(a => ids.Contains(a.Id)));
    }

    private void Place(ArrowPiece arrow)
    {
        _arrows.Add(arrow.Id, arrow);
        foreach (var cell in arrow.Cells)
        {
            _occupancy[cell.X, cell.Y] = arrow.Id;
        }
    }

    private void Remove(int arrowId)
    {
        var arrow = _arrows[arrowId];
        foreach (var cell in arrow.Cells)
        {
            _occupancy[cell.X, cell.Y] = null;
        }

        _arrows.Remove(arrowId);
    }
}
