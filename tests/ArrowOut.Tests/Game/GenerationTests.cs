using ArrowOut.Game;
using ArrowOut.Game.Generation;
using ArrowOut.Game.Solving;

namespace ArrowOut.Tests.Game;

public class GenerationTests
{
    [Theory]
    [InlineData(3, 3, 3)]
    [InlineData(5, 5, 4)]
    [InlineData(8, 6, 6)]
    [InlineData(12, 12, 10)]
    [InlineData(16, 16, 12)]
    public void Generator_FillsEveryCellAndStaysSolvable(int width, int height, int maxLength)
    {
        var solver = new GreedySolver();

        for (var seed = 0; seed < 20; seed++)
        {
            var arrows = new LevelGenerator(new Random(seed)).Generate(new GeneratorSettings
            {
                Width = width,
                Height = height,
                MinLength = 2,
                MaxLength = maxLength,
            });

            Assert.Empty(BoardValidator.Validate(width, height, arrows));
            Assert.Equal(width * height, arrows.Sum(a => a.Length));
            Assert.True(solver.Solve(Board.Create(width, height, arrows)).IsSolvable, $"seed {seed}");
        }
    }

    [Fact]
    public void Generator_PartialFillStopsEarly()
    {
        var arrows = new LevelGenerator(new Random(3)).Generate(new GeneratorSettings { Width = 8, Height = 8, TargetFill = 0.4 });

        Assert.InRange(arrows.Sum(a => a.Length), 26, 63);
    }

    [Fact]
    public void Generator_IsDeterministicForASeed()
    {
        var settings = new GeneratorSettings { Width = 7, Height = 7 };

        var first = new LevelGenerator(new Random(42)).Generate(settings);
        var second = new LevelGenerator(new Random(42)).Generate(settings);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generator_RejectsInvalidSettings()
    {
        var generator = new LevelGenerator(new Random(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Width = 2, Height = 5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Width = 5, Height = 5, TargetFill = 1.5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Width = 5, Height = 5, MinLength = 4, MaxLength = 2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Width = 5, Height = 5, BendChance = 2 }));
    }
}
