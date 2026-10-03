namespace ArrowOut.Game.Generation;

// Saved as a number in the database, so don't change the order.
public enum ChallengeKind
{
    // Around 40 arrows on a small board.
    Easy = 0,

    // Around 200 arrows. (Was called Medium, same value.)
    Normal = 1,

    // 400 to 700 arrows on a huge board.
    Hard = 2,

    // The separate "Challenge game": 600 to 1000 arrows. Never picked at random.
    Challenge = 3,
}

// Makes the game boards. Every cell is filled, the arrows are mostly long and packed next
// to each other, and they point all over the place so they block each other in long chains.
// That way you can't just clear the board from the edges inwards.
// Each kind has its own board size and arrow lengths.
// Same seed and kind = same board.
public static class ChallengeGenerator
{
    public const int Lives = 3;

    private const int MaxRetries = 8;

    private static readonly IReadOnlyDictionary<ChallengeKind, Recipe> Recipes = new Dictionary<ChallengeKind, Recipe>
    {
        [ChallengeKind.Easy] = new(MinSide: 16, MaxSide: 16, MinLength: 3, MaxLength: 12, MinArrows: 32, Growth: 1, Tangle: 0.6),
        [ChallengeKind.Normal] = new(MinSide: 41, MaxSide: 43, MinLength: 4, MaxLength: 20, MinArrows: 180, Growth: 1, Tangle: 0.85),
        [ChallengeKind.Hard] = new(MinSide: 66, MaxSide: 82, MinLength: 4, MaxLength: 24, MinArrows: 400, Growth: 2, Tangle: 1.0),
        [ChallengeKind.Challenge] = new(MinSide: 80, MaxSide: 92, MinLength: 4, MaxLength: 24, MinArrows: 600, Growth: 2, Tangle: 1.0, MaxArrows: 1000),
    };

    // The fewest arrows a board of this kind can have.
    public static int MinArrows(ChallengeKind kind) => RecipeFor(kind).MinArrows;

    // The most arrows a board of this kind can have.
    public static int MaxArrows(ChallengeKind kind) => RecipeFor(kind).MaxArrows;

    public static ChallengeBlueprint Generate(int seed, ChallengeKind kind = ChallengeKind.Hard)
    {
        var recipe = RecipeFor(kind);
        var random = new Random(seed);
        var side = random.Next(recipe.MinSide, recipe.MaxSide + 1);

        for (var retry = 0; retry < MaxRetries; retry++)
        {
            var arrows = new LevelGenerator(random).Generate(new GeneratorSettings
            {
                Width = side,
                Height = side,
                TargetFill = 1.0,
                MinLength = recipe.MinLength,
                MaxLength = recipe.MaxLength,
                BendChance = 0.3,
                HugWalls = true,
                Tangle = recipe.Tangle,
                MaxAttempts = 2,
            });

            if (arrows.Count > recipe.MaxArrows)
            {
                // Too many arrows, so try again on a slightly smaller board.
                side = Math.Max(side - recipe.Growth, BoardValidator.MinSize);
                continue;
            }

            if (arrows.Count >= recipe.MinArrows)
            {
                return new ChallengeBlueprint(seed, kind, side, side, Lives, arrows);
            }

            // Not enough arrows fit, so try again on a slightly bigger board.
            side = Math.Min(side + recipe.Growth, BoardValidator.MaxSize);
        }

        throw new InvalidOperationException($"Could not build a {kind} game with {recipe.MinArrows} to {recipe.MaxArrows} arrows (seed {seed}).");
    }

    private static Recipe RecipeFor(ChallengeKind kind) =>
        Recipes.TryGetValue(kind, out var recipe)
            ? recipe
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown game kind.");

    private sealed record Recipe(
        int MinSide, int MaxSide, int MinLength, int MaxLength, int MinArrows, int Growth, double Tangle, int MaxArrows = int.MaxValue);
}

public sealed record ChallengeBlueprint(int Seed, ChallengeKind Kind, int Width, int Height, int MaxLives, IReadOnlyList<ArrowPiece> Arrows);
