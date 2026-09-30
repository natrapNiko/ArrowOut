namespace ArrowOut.Game;

// Checks the layout is valid (bounds, overlaps...). Doesn't check if it can be solved.
public static class BoardValidator
{
    public const int MinSize = 3;
    public const int MaxSize = 96;
    public const int MaxArrows = MaxSize * MaxSize;

    public static IReadOnlyList<string> Validate(int width, int height, IEnumerable<ArrowPiece> arrows)
    {
        ArgumentNullException.ThrowIfNull(arrows);

        var errors = new List<string>();

        if (width is < MinSize or > MaxSize)
        {
            errors.Add($"Width must be between {MinSize} and {MaxSize}.");
        }

        if (height is < MinSize or > MaxSize)
        {
            errors.Add($"Height must be between {MinSize} and {MaxSize}.");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var list = arrows.ToList();
        if (list.Count == 0)
        {
            errors.Add("A board needs at least one arrow.");
        }

        if (list.Count > MaxArrows)
        {
            errors.Add($"A board can hold at most {MaxArrows} arrows.");
        }

        var ids = new HashSet<int>();
        var occupied = new Dictionary<GridPoint, int>();

        foreach (var arrow in list)
        {
            if (!ids.Add(arrow.Id))
            {
                errors.Add($"Arrow id {arrow.Id} is used more than once.");
            }

            foreach (var cell in arrow.Cells)
            {
                if (!IsInside(width, height, cell))
                {
                    errors.Add($"Arrow {arrow} extends outside the board at {cell}.");
                    break;
                }

                if (occupied.TryGetValue(cell, out var otherId))
                {
                    errors.Add($"Arrow {arrow} overlaps arrow #{otherId} at {cell}.");
                    break;
                }

                occupied[cell] = arrow.Id;
            }
        }

        return errors;
    }

    public static bool IsInside(int width, int height, GridPoint cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
}
