using ArrowOut.Data.Models;

namespace ArrowOut.Tests.Data;

public class PlayerProgressTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    public void CalculateStars_DependsOnMistakes(int mistakes, int expected)
    {
        Assert.Equal(expected, PlayerProgress.CalculateStars(mistakes));
    }

    [Fact]
    public void CalculateStars_RejectsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerProgress.CalculateStars(-1));
    }

    [Fact]
    public void RecordWin_FirstWinSetsCompletionAndBest()
    {
        var progress = new PlayerProgress("u", 1);

        var isBest = progress.RecordWin(1, Now);

        Assert.True(isBest);
        Assert.True(progress.IsCompleted);
        Assert.Equal(2, progress.Stars);
        Assert.Equal(1, progress.BestMistakes);
        Assert.Equal(Now, progress.FirstCompletedOn);
        Assert.Equal(1, progress.Completions);
    }

    [Fact]
    public void RecordWin_WorseRunNeverLowersTheBest()
    {
        var progress = new PlayerProgress("u", 1);
        progress.RecordWin(0, Now);

        var isBest = progress.RecordWin(2, Now.AddHours(1));

        Assert.False(isBest);
        Assert.Equal(3, progress.Stars);
        Assert.Equal(0, progress.BestMistakes);
        Assert.Equal(Now, progress.FirstCompletedOn);
        Assert.Equal(2, progress.Completions);
    }

    [Fact]
    public void RecordAttempt_IncrementsCounter()
    {
        var progress = new PlayerProgress("u", 1);

        progress.RecordAttempt(Now);
        progress.RecordAttempt(Now);

        Assert.Equal(2, progress.Attempts);
        Assert.False(progress.IsCompleted);
    }

    [Fact]
    public void Constructor_RequiresUser()
    {
        Assert.Throws<ArgumentException>(() => new PlayerProgress(" ", 1));
    }
}
