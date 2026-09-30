using ArrowOut.Data;
using ArrowOut.Data.Common;
using ArrowOut.Game.Generation;
using ArrowOut.Services.Analytics;
using ArrowOut.Services.Leaderboard;
using ArrowOut.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArrowOut.Tests.Services;

public class LeaderboardAndAnalyticsTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Leaderboard_RanksByChallengesWonThenStars()
    {
        await TestDb.AddUserAsync(_db, "alice", "Alice");
        await TestDb.AddUserAsync(_db, "bob", "Bob");
        await TestDb.AddUserAsync(_db, "carol", "Carol");
        await TestDb.AddUserAsync(_db, "dave", "Dave");

        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 2);
        await TestDb.AddChallengeAsync(_db, "bob", wonWithMistakes: 0);
        await TestDb.AddChallengeAsync(_db, "bob", wonWithMistakes: 0);
        await TestDb.AddChallengeAsync(_db, "carol", wonWithMistakes: 0);
        await TestDb.AddChallengeAsync(_db, "dave"); // started, never won: not ranked

        var page = await new LeaderboardService(_db).GetPageAsync(ChallengeKind.Easy, 1, 10);

        Assert.Equal(["Bob", "Carol", "Alice"], page.Items.Select(e => e.PlayerName));
        Assert.Equal([2, 1, 1], page.Items.Select(e => e.ChallengesWon));
        Assert.Equal([1, 2, 3], page.Items.Select(e => e.Rank));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Leaderboard_LeavesOutAdministrators()
    {
        await TestDb.AddUserAsync(_db, "admin", "Admin");
        await TestDb.AddUserAsync(_db, "alice", "Alice");
        var role = new IdentityRole(Roles.Administrator);
        _db.Roles.Add(role);
        _db.UserRoles.Add(new IdentityUserRole<string> { UserId = "admin", RoleId = role.Id });
        await _db.SaveChangesAsync();

        // The admin has the better score, but test games shouldn't count.
        await TestDb.AddChallengeAsync(_db, "admin", wonWithMistakes: 0);
        await TestDb.AddChallengeAsync(_db, "admin", wonWithMistakes: 0);
        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 2);

        var page = await new LeaderboardService(_db).GetPageAsync(ChallengeKind.Easy, 1, 10);

        Assert.Equal(["Alice"], page.Items.Select(e => e.PlayerName));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Leaderboard_RanksByPoints_ThenStars()
    {
        await TestDb.AddUserAsync(_db, "alice", "Alice");
        await TestDb.AddUserAsync(_db, "bob", "Bob");
        await TestDb.AddUserAsync(_db, "carol", "Carol");

        // Medium wins are 4 points each no matter how many crashes. Alice and Bob have the same
        // points, but Bob never crashed (more stars) so he's ahead.
        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 2, kind: ChallengeKind.Medium);
        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 2, kind: ChallengeKind.Medium);
        await TestDb.AddChallengeAsync(_db, "bob", wonWithMistakes: 0, kind: ChallengeKind.Medium);
        await TestDb.AddChallengeAsync(_db, "bob", wonWithMistakes: 0, kind: ChallengeKind.Medium);
        await TestDb.AddChallengeAsync(_db, "carol", wonWithMistakes: 0, kind: ChallengeKind.Medium);

        var page = await new LeaderboardService(_db).GetPageAsync(ChallengeKind.Medium, 1, 10);

        Assert.Equal(
            [("Bob", 8, 2), ("Alice", 8, 2), ("Carol", 4, 1)],
            page.Items.Select(e => (e.PlayerName, e.TotalPoints, e.ChallengesWon)));
    }

    [Fact]
    public async Task Leaderboard_KeepsASeparateRankingPerChallengeKind()
    {
        await TestDb.AddUserAsync(_db, "alice", "Alice");
        await TestDb.AddUserAsync(_db, "bob", "Bob");

        // Alice farms easy boards, Bob wins the one hard board.
        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 0, kind: ChallengeKind.Easy);
        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 0, kind: ChallengeKind.Easy);
        await TestDb.AddChallengeAsync(_db, "alice", wonWithMistakes: 0, kind: ChallengeKind.Easy);
        await TestDb.AddChallengeAsync(_db, "bob", wonWithMistakes: 1, kind: ChallengeKind.Hard);

        var service = new LeaderboardService(_db);
        var easy = await service.GetPageAsync(ChallengeKind.Easy, 1, 10);
        var medium = await service.GetPageAsync(ChallengeKind.Medium, 1, 10);
        var hard = await service.GetPageAsync(ChallengeKind.Hard, 1, 10);

        Assert.Equal([("Alice", 3)], easy.Items.Select(e => (e.PlayerName, e.ChallengesWon)));
        Assert.Empty(medium.Items);
        Assert.Equal([("Bob", 1)], hard.Items.Select(e => (e.PlayerName, e.ChallengesWon)));
    }

    [Fact]
    public async Task Leaderboard_SecondPageContinuesRanking()
    {
        for (var i = 0; i < 5; i++)
        {
            await TestDb.AddUserAsync(_db, $"user{i}", $"User {i}");
            await TestDb.AddChallengeAsync(_db, $"user{i}", wonWithMistakes: 0);
        }

        var page = await new LeaderboardService(_db).GetPageAsync(ChallengeKind.Easy, 2, 2);

        Assert.Equal([3, 4], page.Items.Select(e => e.Rank));
    }

    [Theory]
    [InlineData("Neo", "someone@example.com", "Neo")]
    [InlineData(null, "someone@example.com", "so***")]
    [InlineData(null, "ab@example.com", "Player")]
    [InlineData(" ", null, "Player")]
    public void PublicName_NeverLeaksTheEmail(string? displayName, string? userName, string expected)
    {
        Assert.Equal(expected, LeaderboardService.PublicName(displayName, userName));
    }

    [Fact]
    public void Tracker_PseudonymisesUserIds()
    {
        var queue = new AnalyticsQueue(10);
        var tracker = new ChannelAnalyticsTracker(queue, TimeProvider.System, NullLogger<ChannelAnalyticsTracker>.Instance);

        tracker.Track("level_completed", "user-123", new Dictionary<string, object?> { ["level_number"] = 4 });

        Assert.True(queue.Channel.Reader.TryRead(out var analyticsEvent));
        Assert.Equal("level_completed", analyticsEvent!.Name);
        Assert.NotEqual("user-123", analyticsEvent.DistinctId);
        Assert.Equal(32, analyticsEvent.DistinctId.Length);
        Assert.Equal(ChannelAnalyticsTracker.Pseudonymise("user-123"), analyticsEvent.DistinctId);
    }

    [Fact]
    public void Tracker_DropsOldestWhenFull_AndNeverThrows()
    {
        var queue = new AnalyticsQueue(10);
        var tracker = new ChannelAnalyticsTracker(queue, TimeProvider.System, NullLogger<ChannelAnalyticsTracker>.Instance);

        for (var i = 0; i < 50; i++)
        {
            tracker.Track("e" + i, "u");
        }

        Assert.True(queue.Channel.Reader.TryRead(out var first));
        Assert.Equal("e40", first!.Name);
    }

    [Fact]
    public void Tracker_IgnoresBlankInput()
    {
        var queue = new AnalyticsQueue(10);
        var tracker = new ChannelAnalyticsTracker(queue, TimeProvider.System, NullLogger<ChannelAnalyticsTracker>.Instance);

        tracker.Track("", "u");
        tracker.Track("e", " ");

        Assert.False(queue.Channel.Reader.TryRead(out _));
    }
}
