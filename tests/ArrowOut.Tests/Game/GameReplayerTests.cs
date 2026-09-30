using ArrowOut.Game;
using ArrowOut.Game.Replay;

namespace ArrowOut.Tests.Game;

public class GameReplayerTests
{
    // Row of three right-pointing arrows: 3 is free, 2 is behind 3, 1 is behind 2.
    private static Board Queue() => Board.Create(5, 3,
    [
        new ArrowPiece(1, new GridPoint(1, 1), Direction.Right, 1),
        new ArrowPiece(2, new GridPoint(2, 1), Direction.Right, 1),
        new ArrowPiece(3, new GridPoint(3, 1), Direction.Right, 1),
    ]);

    [Fact]
    public void PerfectRun_IsWinWithoutMistakes()
    {
        var result = GameReplayer.Replay(Queue(), [3, 2, 1], maxLives: 3);

        Assert.True(result.IsValid);
        Assert.True(result.IsWin);
        Assert.Equal(0, result.Mistakes);
    }

    [Fact]
    public void Collisions_CountAsMistakes()
    {
        var result = GameReplayer.Replay(Queue(), [1, 2, 3, 2, 1], maxLives: 3);

        Assert.True(result.IsWin);
        Assert.Equal(2, result.Mistakes);
    }

    [Fact]
    public void RunningOutOfLives_IsNotAWin()
    {
        var result = GameReplayer.Replay(Queue(), [1, 1, 1], maxLives: 3);

        Assert.True(result.IsValid);
        Assert.False(result.IsWin);
        Assert.True(result.LivesExhausted);
    }

    [Fact]
    public void TapsAfterLosingAllLives_AreRejected()
    {
        var result = GameReplayer.Replay(Queue(), [1, 1, 1, 3, 2, 1], maxLives: 3);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.InvalidTapIndex);
    }

    [Fact]
    public void UnknownArrow_IsRejected()
    {
        var result = GameReplayer.Replay(Queue(), [99], maxLives: 3);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.InvalidTapIndex);
    }

    [Fact]
    public void TappingRemovedArrowAgain_IsRejected()
    {
        var result = GameReplayer.Replay(Queue(), [3, 3], maxLives: 3);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TapsAfterClearing_AreRejected()
    {
        var board = Board.Create(3, 3, [new ArrowPiece(1, new GridPoint(1, 1), Direction.Up, 1)]);

        var result = GameReplayer.Replay(board, [1, 1], maxLives: 3);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void IncompleteRun_IsValidButNotAWin()
    {
        var result = GameReplayer.Replay(Queue(), [3], maxLives: 3);

        Assert.True(result.IsValid);
        Assert.False(result.IsWin);
        Assert.Equal(2, result.RemainingArrows);
    }

    [Fact]
    public void Replay_DoesNotMutateTheInputBoard()
    {
        var board = Queue();

        GameReplayer.Replay(board, [3, 2, 1], maxLives: 3);

        Assert.Equal(3, board.Count);
    }

    [Fact]
    public void TooManyTaps_AreRejected()
    {
        var taps = Enumerable.Repeat(1, GameReplayer.MaxTaps + 1).ToList();

        Assert.False(GameReplayer.Replay(Queue(), taps, maxLives: 3).IsValid);
    }
}
