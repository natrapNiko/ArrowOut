using ArrowOut.Services.Models;

namespace ArrowOut.Services.Levels;

// The unlock rule: the first published level is always open, and every other one opens once
// you've beaten the one before it. You can always replay levels you've finished.
public static class LevelAccessCalculator
{
    public static IReadOnlyDictionary<int, LevelStatus> Compute(
        IReadOnlyList<(int Id, int Number)> publishedLevels,
        IReadOnlySet<int> completedLevelIds,
        bool unlockAll = false)
    {
        ArgumentNullException.ThrowIfNull(publishedLevels);
        ArgumentNullException.ThrowIfNull(completedLevelIds);

        var statuses = new Dictionary<int, LevelStatus>(publishedLevels.Count);
        var previousCompleted = true;

        foreach (var (id, _) in publishedLevels.OrderBy(l => l.Number))
        {
            var completed = completedLevelIds.Contains(id);
            statuses[id] = completed
                ? LevelStatus.Completed
                : (previousCompleted || unlockAll ? LevelStatus.Unlocked : LevelStatus.Locked);

            previousCompleted = completed;
        }

        return statuses;
    }
}
