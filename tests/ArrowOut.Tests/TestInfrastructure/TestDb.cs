using ArrowOut.Data;
using ArrowOut.Data.Models;
using ArrowOut.Game;
using ArrowOut.Game.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Tests.TestInfrastructure;

// A fresh in-memory database for each test, plus some helpers to set up data.
internal static class TestDb
{
    public const string PlayerId = "player-1";
    public const string OtherPlayerId = "player-2";

    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public static async Task<ApplicationUser> AddUserAsync(ApplicationDbContext db, string id, string? displayName = null)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = $"{id}@example.com",
            Email = $"{id}@example.com",
            DisplayName = displayName,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // Solution: tap the arrow at (3,1), then (2,1), then (1,1).
    public static Level QueueLevel(int number, bool published = true, string? name = null)
    {
        var level = new Level
        {
            Number = number,
            Name = name ?? $"Queue {number}",
            Width = 5,
            Height = 3,
            MaxLives = 3,
            Difficulty = Difficulty.Easy,
            IsPublished = published,
        };

        level.ReplaceArrows(
        [
            new Arrow { X = 1, Y = 1, Direction = Direction.Right, Length = 1 },
            new Arrow { X = 2, Y = 1, Direction = Direction.Right, Length = 1 },
            new Arrow { X = 3, Y = 1, Direction = Direction.Right, Length = 1 },
        ]);

        return level;
    }

    public static async Task<List<Level>> AddLevelsAsync(ApplicationDbContext db, params Level[] levels)
    {
        db.Levels.AddRange(levels);
        await db.SaveChangesAsync();
        return levels.ToList();
    }

    // Arrow ids of a queue level in the order you'd win (front to back).
    public static IReadOnlyList<int> WinningTaps(Level level) =>
        level.Arrows.OrderByDescending(a => a.X).Select(a => a.Id).ToList();

    // Tiny challenge with the same queue: ids 1, 2, 3 at (1,1), (2,1), (3,1), all pointing right,
    // so the only way to win is 3, 2, 1 (see ChallengeWinningTaps).
    public static Challenge QueueChallenge(string ownerId, ChallengeKind kind = ChallengeKind.Easy) => new(
        ownerId,
        kind,
        seed: 1,
        width: 5,
        height: 3,
        maxLives: 3,
        [
            new ArrowPiece(1, new GridPoint(1, 1), Direction.Right, 1),
            new ArrowPiece(2, new GridPoint(2, 1), Direction.Right, 1),
            new ArrowPiece(3, new GridPoint(3, 1), Direction.Right, 1),
        ]);

    public static readonly IReadOnlyList<int> ChallengeWinningTaps = [3, 2, 1];

    public static async Task<Challenge> AddChallengeAsync(
        ApplicationDbContext db, string ownerId, int? wonWithMistakes = null, ChallengeKind kind = ChallengeKind.Easy)
    {
        var challenge = QueueChallenge(ownerId, kind);
        if (wonWithMistakes is { } mistakes)
        {
            challenge.RecordWin(mistakes, DateTime.UtcNow);
        }

        db.Challenges.Add(challenge);
        await db.SaveChangesAsync();
        return challenge;
    }

    public static async Task CompleteAsync(ApplicationDbContext db, string userId, Level level, int mistakes = 0)
    {
        var progress = new PlayerProgress(userId, level.Id);
        progress.RecordWin(mistakes, DateTime.UtcNow);
        db.PlayerProgress.Add(progress);
        await db.SaveChangesAsync();
    }
}
