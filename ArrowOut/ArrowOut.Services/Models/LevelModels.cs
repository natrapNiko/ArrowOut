using System.ComponentModel.DataAnnotations;
using ArrowOut.Game;

namespace ArrowOut.Services.Models;

public enum LevelStatus
{
    Locked = 0,
    Unlocked = 1,
    Completed = 2,
}

public enum LevelStatusFilter
{
    All = 0,
    Unlocked = 1,
    Completed = 2,
    Locked = 3,
}

// Filters for the level list, read from the query string.
public sealed class LevelQuery
{
    [StringLength(60)]
    public string? Search { get; set; }

    public Difficulty? Difficulty { get; set; }

    public LevelStatusFilter Status { get; set; } = LevelStatusFilter.All;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, Paging.MaxPageSize)]
    public int PageSize { get; set; } = 24;
}

public sealed record LevelListItem(
    int Id,
    int Number,
    string Name,
    Difficulty Difficulty,
    int Width,
    int Height,
    int ArrowCount,
    LevelStatus Status,
    int Stars);

public sealed record LevelPlayInfo(
    int Id,
    int Number,
    string Name,
    Difficulty Difficulty,
    int MaxLives,
    LevelStatus Status,
    int BestStars,
    int? PreviousLevelNumber,
    int? NextLevelNumber);

public sealed record CellDto(int X, int Y);

// An arrow as the browser gets it. Cells go from the head back (for bent arrows).
public sealed record ArrowDto(int Id, int X, int Y, Direction Direction, int Length, IReadOnlyList<CellDto> Cells)
{
    public static ArrowDto FromPiece(ArrowPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return new ArrowDto(
            piece.Id,
            piece.Head.X,
            piece.Head.Y,
            piece.Direction,
            piece.Length,
            piece.Cells.Select(c => new CellDto(c.X, c.Y)).ToList());
    }
}

public sealed record LevelBoardDto(
    int Id,
    int Number,
    string Name,
    int Width,
    int Height,
    int MaxLives,
    Difficulty Difficulty,
    IReadOnlyList<ArrowDto> Arrows);

public sealed record ProgressSummary(
    int CompletedLevels,
    int TotalLevels,
    int TotalStars,
    int MaxStars,
    int? NextLevelNumber)
{
    public int PercentComplete => TotalLevels == 0 ? 0 : (int)Math.Round(CompletedLevels * 100.0 / TotalLevels);
}

public sealed record LeaderboardEntry(int Rank, string PlayerName, int TotalPoints, int ChallengesWon, int TotalStars, int TotalMistakes);
