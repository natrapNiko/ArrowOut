namespace ArrowOut.Game.Solving;

// Picks the free arrow that frees up the most other arrows (lowest id on a tie).
// Any free arrow is a safe move anyway (see GreedySolver), so this only makes the hint
// more useful, it can never make it wrong.
public sealed class UnblockingHintProvider : IHintProvider
{
    public int? GetHint(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var freeIds = board.FreeArrowIds().ToList();
        if (freeIds.Count == 0)
        {
            return null;
        }

        var bestId = freeIds[0];
        var bestGain = -1;

        foreach (var id in freeIds)
        {
            var probe = board.Clone();
            probe.Move(id);
            var gain = probe.FreeArrowIds().Count() - (freeIds.Count - 1);

            if (gain > bestGain)
            {
                bestGain = gain;
                bestId = id;
            }
        }

        return bestId;
    }
}
