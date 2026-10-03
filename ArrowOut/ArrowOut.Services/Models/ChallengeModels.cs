namespace ArrowOut.Services.Models;

// What the play page needs before it loads the board.
public sealed record ChallengeInfo(int Id, ArrowOut.Game.Generation.ChallengeKind Kind, int Width, int Height, int ArrowCount, int MaxLives, bool IsCompleted, int Stars);

// The board sent to the browser (GET /api/games/{id}).
public sealed record ChallengeBoardDto(int Id, int Width, int Height, int MaxLives, IReadOnlyList<ArrowDto> Arrows);

// A checked win. Points = what the board is worth (Easy 1, Normal 4, Hard 10, Challenge 20).
// PointsGained = how much your total went up (0 if you'd already won this board).
// TotalPoints = your new total over all games (what the leaderboard shows).
public sealed record ChallengeCompletionResult(
    int ChallengeId, int Stars, int Mistakes, bool IsNewBest, bool IsFirstCompletion,
    int Points, int PointsGained, int HintsUsed, int TotalPoints);

// A player's stats across all games. UnfinishedId is the newest board they haven't won yet.
public sealed record ChallengeSummary(int Played, int Won, int TotalStars, int TotalPoints, int? UnfinishedId);
