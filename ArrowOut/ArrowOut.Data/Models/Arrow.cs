using System.Globalization;
using System.Text;
using ArrowOut.Data.Models.Common;
using ArrowOut.Game;

namespace ArrowOut.Data.Models;

// One arrow in an admin level. X/Y is the head. Path has all the cells ("x,y;x,y;...")
// starting from the head, which is how bent arrows are stored.
public class Arrow : BaseModel<int>
{
    public const int PathMaxLength = 512;

    public int LevelId { get; set; }

    public virtual Level Level { get; set; } = null!;

    // Head column.
    public int X { get; set; }

    // Head row.
    public int Y { get; set; }

    public Direction Direction { get; set; }

    // Number of cells. Kept as its own column so the database can check it.
    public int Length { get; set; } = 1;

    // Cells from the head back, e.g. "3,4;3,5;2,5". Empty means a straight arrow.
    public string Path { get; set; } = string.Empty;

    public ArrowPiece ToPiece() =>
        string.IsNullOrEmpty(Path)
            ? new ArrowPiece(Id, new GridPoint(X, Y), Direction, Length)
            : new ArrowPiece(Id, DecodePath(Path), Direction);

    public static Arrow FromPiece(ArrowPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        return new Arrow
        {
            X = piece.Head.X,
            Y = piece.Head.Y,
            Direction = piece.Direction,
            Length = piece.Length,
            Path = EncodePath(piece.Cells),
        };
    }

    public static string EncodePath(IEnumerable<GridPoint> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var builder = new StringBuilder();
        foreach (var cell in cells)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append(CultureInfo.InvariantCulture, $"{cell.X},{cell.Y}");
        }

        return builder.ToString();
    }

    public static IReadOnlyList<GridPoint> DecodePath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return path
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair =>
            {
                var parts = pair.Split(',');
                if (parts.Length != 2
                    || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y))
                {
                    throw new FormatException($"Invalid arrow path segment '{pair}'.");
                }

                return new GridPoint(x, y);
            })
            .ToList();
    }
}
