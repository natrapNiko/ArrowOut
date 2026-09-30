using System.Globalization;
using System.Text;
using ArrowOut.Game;
using ArrowOut.Game.Generation;

namespace ArrowOut.Web.Areas.Identity.Pages.Account;

// Text for the panel next to the sign-in and sign-up forms.
public sealed record AuthAsideModel(string Title, string Text);

// A small maze made by the real generator for the sign-in pages. The seed is fixed, so it
// looks the same every time and only gets built once.
public static class AuthMaze
{
    public const int Size = 10;
    public const int Cell = 20;

    private static readonly Lazy<IReadOnlyList<MazeArrow>> Arrows = new(Build);

    public static IReadOnlyList<MazeArrow> Get() => Arrows.Value;

    private static IReadOnlyList<MazeArrow> Build()
    {
        var pieces = new LevelGenerator(new Random(20_260_929)).Generate(new GeneratorSettings
        {
            Width = Size,
            Height = Size,
            MinLength = 3,
            MaxLength = 14,
            BendChance = 0.3,
            HugWalls = true,
        });

        return pieces.Select(ToSvg).ToList();
    }

    private static MazeArrow ToSvg(ArrowPiece piece)
    {
        static string Num(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
        static (double X, double Y) Center(GridPoint p) => ((p.X * Cell) + (Cell / 2.0), (p.Y * Cell) + (Cell / 2.0));

        var (hx, hy) = Center(piece.Head);
        var delta = piece.Direction.ToDelta();
        var (dx, dy) = (delta.X, delta.Y);
        var tip = (X: hx + (dx * 8), Y: hy + (dy * 8));

        // The line goes from the tail through every cell to just before the tip, so it runs into
        // the head like a hand-drawn arrow.
        var path = new StringBuilder();
        foreach (var cell in piece.Cells.Reverse())
        {
            var (x, y) = Center(cell);
            path.Append(path.Length == 0 ? 'M' : 'L').Append(Num(x)).Append(' ').Append(Num(y)).Append(' ');
        }

        path.Append('L').Append(Num(tip.X - dx)).Append(' ').Append(Num(tip.Y - dy));

        // The head is an open "V", with the arms starting just behind the head cell's centre.
        var baseX = hx - dx;
        var baseY = hy - dy;
        var head = $"M{Num(baseX - (dy * 5))} {Num(baseY + (dx * 5))} L{Num(tip.X)} {Num(tip.Y)} L{Num(baseX + (dy * 5))} {Num(baseY - (dx * 5))}";

        return new MazeArrow(path.ToString(), head);
    }
}

// Path is the line, Head is the "V". Both are SVG path data.
public sealed record MazeArrow(string Path, string Head);
