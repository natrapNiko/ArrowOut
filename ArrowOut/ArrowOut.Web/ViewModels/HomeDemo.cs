using System.Text.Json;
using ArrowOut.Game.Generation;

namespace ArrowOut.Web.ViewModels;

// The boards that play themselves on the home page. A few small tangled ones from the real
// generator (fixed seeds, built once), passed to home-demo.js as JSON in a data attribute.
public static class HomeDemo
{
    public const int Size = 7;

    private static readonly int[] Seeds = [1990, 1994, 1997];

    private static readonly Lazy<string> BoardsJson = new(Build);

    // [{ width, height, arrows: [{ id, direction, cells: [{ x, y }] }] }, ...]
    public static string Json => BoardsJson.Value;

    private static string Build()
    {
        var boards = Seeds.Select(seed =>
        {
            var arrows = new LevelGenerator(new Random(seed)).Generate(new GeneratorSettings
            {
                Width = Size,
                Height = Size,
                MinLength = 2,
                MaxLength = 9,
                BendChance = 0.3,
                HugWalls = true,
                Tangle = 0.6,
            });

            return new
            {
                width = Size,
                height = Size,
                arrows = arrows.Select(a => new
                {
                    id = a.Id,
                    direction = (int)a.Direction,
                    cells = a.Cells.Select(c => new { x = c.X, y = c.Y }),
                }),
            };
        });

        return JsonSerializer.Serialize(boards);
    }
}
