namespace ArrowOut.Game.Solving;

public interface IPuzzleSolver
{
    // Can this board be cleared? Doesn't change the board you pass in.
    SolveResult Solve(Board board);
}
