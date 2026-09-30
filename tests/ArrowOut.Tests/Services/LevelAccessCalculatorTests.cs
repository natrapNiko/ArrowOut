using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;

namespace ArrowOut.Tests.Services;

public class LevelAccessCalculatorTests
{
    private static readonly IReadOnlyList<(int Id, int Number)> Levels = [(10, 1), (20, 2), (30, 3), (40, 5)];

    [Fact]
    public void NewPlayer_OnlyFirstLevelIsOpen()
    {
        var statuses = LevelAccessCalculator.Compute(Levels, new HashSet<int>());

        Assert.Equal(LevelStatus.Unlocked, statuses[10]);
        Assert.Equal(LevelStatus.Locked, statuses[20]);
        Assert.Equal(LevelStatus.Locked, statuses[40]);
    }

    [Fact]
    public void CompletingALevel_UnlocksTheNextPublishedOne_EvenAcrossNumberGaps()
    {
        var statuses = LevelAccessCalculator.Compute(Levels, new HashSet<int> { 10, 20, 30 });

        Assert.Equal(LevelStatus.Completed, statuses[30]);
        Assert.Equal(LevelStatus.Unlocked, statuses[40]);
    }

    [Fact]
    public void CompletedLevels_StayCompletedEvenIfEarlierOnesAreNot()
    {
        // e.g. an admin added a new level 2 after the player had already beaten level 3.
        var statuses = LevelAccessCalculator.Compute(Levels, new HashSet<int> { 10, 30 });

        Assert.Equal(LevelStatus.Unlocked, statuses[20]);
        Assert.Equal(LevelStatus.Completed, statuses[30]);
        Assert.Equal(LevelStatus.Unlocked, statuses[40]);
    }

    [Fact]
    public void UnlockAll_OpensEverything()
    {
        var statuses = LevelAccessCalculator.Compute(Levels, new HashSet<int>(), unlockAll: true);

        Assert.DoesNotContain(LevelStatus.Locked, statuses.Values);
    }

    [Fact]
    public void Order_IsByNumberNotByInputOrder()
    {
        var shuffled = new List<(int, int)> { (30, 3), (10, 1), (20, 2) };

        var statuses = LevelAccessCalculator.Compute(shuffled, new HashSet<int> { 10 });

        Assert.Equal(LevelStatus.Unlocked, statuses[20]);
        Assert.Equal(LevelStatus.Locked, statuses[30]);
    }
}
