namespace ArrowOut.Game.Generation;

public sealed record GeneratorSettings
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    // How much of the board gets covered, 0.1 to 1.0. 1.0 means every cell is used.
    public double TargetFill { get; init; } = 1.0;

    public int MinLength { get; init; } = 2;

    public int MaxLength { get; init; } = 6;

    // Chance (0 to 1) that the body turns at each step. Higher = more of a maze.
    public double BendChance { get; init; } = 0.45;

    // Bodies grow along walls and other arrows, so you get long arrows packed side by side
    // instead of random squiggles.
    public bool HugWalls { get; init; }

    // 0 to 1. At 0 arrows point at the closest edge, so the board clears from the outside in.
    // Higher values point them across the board, so they block each other in long chains.
    // The board is solvable either way.
    public double Tangle { get; init; }

    // How many full boards to try. The one with the fewest gaps wins.
    public int MaxAttempts { get; init; } = 40;

    public void EnsureValid()
    {
        if (Width is < BoardValidator.MinSize or > BoardValidator.MaxSize
            || Height is < BoardValidator.MinSize or > BoardValidator.MaxSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Width), $"Board sides must be between {BoardValidator.MinSize} and {BoardValidator.MaxSize}.");
        }

        if (TargetFill is < 0.1 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(TargetFill), TargetFill, "Fill must be between 0.1 and 1.0.");
        }

        if (MinLength < ArrowPiece.MinLength || MaxLength > ArrowPiece.MaxLength || MinLength > MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(MinLength), "Invalid arrow length range.");
        }

        if (BendChance is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BendChance), BendChance, "Bend chance must be between 0 and 1.");
        }

        if (Tangle is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Tangle), Tangle, "Tangle must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(MaxAttempts, 1);
    }
}
