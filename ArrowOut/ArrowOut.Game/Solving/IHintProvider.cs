namespace ArrowOut.Game.Solving;

public interface IHintProvider
{
    // Id of an arrow that's safe to tap, or null if nothing can move.
    int? GetHint(Board board);
}
