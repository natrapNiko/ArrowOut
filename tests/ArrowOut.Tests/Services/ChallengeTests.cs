using ArrowOut.Data;
using ArrowOut.Data.Models;
using ArrowOut.Game;
using ArrowOut.Game.Generation;
using ArrowOut.Game.Solving;
using ArrowOut.Services.Analytics;
using ArrowOut.Services.Challenges;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using ArrowOut.Tests.TestInfrastructure;
using ArrowOut.Web.Controllers.Api;
using ArrowOut.Tests.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArrowOut.Tests.Services;

public class ChallengeTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly Mock<IAnalyticsTracker> _analytics = new();

    private ChallengeService CreateService() => new(
        _db, new UnblockingHintProvider(), _analytics.Object, TimeProvider.System, NullLogger<ChallengeService>.Instance);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- Generator ----------

    [Theory]
    [InlineData(1)]
    [InlineData(20_260_928)]
    public void Generate_IsAFullSolvableLabyrinthWithAtLeast400Arrows(int seed)
    {
        var challenge = ChallengeGenerator.Generate(seed);
        var board = Board.Create(challenge.Width, challenge.Height, challenge.Arrows);

        Assert.True(challenge.Arrows.Count >= ChallengeGenerator.MinArrows(ChallengeKind.Hard), $"{challenge.Arrows.Count} arrows");
        Assert.Equal(challenge.Width * challenge.Height, challenge.Arrows.Sum(a => a.Length));
        Assert.True(new GreedySolver().Solve(board).IsSolvable);
    }

    [Fact]
    public void Generate_ChallengeGame_Has600To1000Arrows_AndIsAFullSolvableBoard()
    {
        var challenge = ChallengeGenerator.Generate(7, ChallengeKind.Challenge);

        Assert.InRange(challenge.Arrows.Count, 600, 1000);
        Assert.Equal(challenge.Width * challenge.Height, challenge.Arrows.Sum(a => a.Length));
        Assert.True(new GreedySolver().Solve(Board.Create(challenge.Width, challenge.Height, challenge.Arrows)).IsSolvable);
    }

    [Theory]
    [InlineData(ChallengeKind.Easy, 32, 50)]
    [InlineData(ChallengeKind.Normal, 180, 230)]
    public void Generate_EachKindHasItsArrowCount_AndIsAFullSolvableBoard(ChallengeKind kind, int min, int max)
    {
        for (var seed = 1; seed <= 3; seed++)
        {
            var challenge = ChallengeGenerator.Generate(seed, kind);

            Assert.Equal(kind, challenge.Kind);
            Assert.InRange(challenge.Arrows.Count, min, max);
            Assert.Equal(challenge.Width * challenge.Height, challenge.Arrows.Sum(a => a.Length));
            Assert.True(new GreedySolver().Solve(Board.Create(challenge.Width, challenge.Height, challenge.Arrows)).IsSolvable);
        }
    }

    [Fact]
    public void Generate_HardIsTangled_NotPeelableEdgeByEdge()
    {
        var challenge = ChallengeGenerator.Generate(11, ChallengeKind.Hard);
        var board = Board.Create(challenge.Width, challenge.Height, challenge.Arrows);
        var side = challenge.Width;

        // Arrows pointing at their closest edge are what lets you clear a board from the outside in.
        var towardNearestEdge = challenge.Arrows.Count(a =>
        {
            int[] distance = [a.Head.Y, side - 1 - a.Head.X, side - 1 - a.Head.Y, a.Head.X]; // Up, Right, Down, Left
            return distance[(int)a.Direction] == distance.Min();
        });

        // Keep removing everything that's free. Lots of rounds = long chains of arrows blocking each other.
        var waves = 0;
        for (var work = board.Clone(); !work.IsCleared; waves++)
        {
            var free = work.FreeArrowIds().ToList();
            Assert.NotEmpty(free); // still solvable
            free.ForEach(id => work.Move(id));
        }

        Assert.True(towardNearestEdge < challenge.Arrows.Count / 2, $"{towardNearestEdge} of {challenge.Arrows.Count} point to the nearest edge");
        Assert.True(board.FreeArrowIds().Count() < challenge.Arrows.Count / 10, "only a few arrows can leave at the start");
        Assert.True(waves >= 35, $"{waves} waves");
    }

    [Fact]
    public void Tangle_OutOfRange_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LevelGenerator(new Random(1))
            .Generate(new GeneratorSettings { Width = 8, Height = 8, Tangle = 1.5 }));
    }

    [Fact]
    public void Generate_RejectsUnknownKinds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChallengeGenerator.Generate(1, (ChallengeKind)9));
    }

    [Fact]
    public void Generate_IsMostlyLongWindingArrows()
    {
        var arrows = ChallengeGenerator.Generate(7).Arrows;

        Assert.True(arrows.Average(a => a.Length) >= 8, $"average length {arrows.Average(a => a.Length):F1}");
        Assert.True(arrows.Count(a => a.Length <= 3) < arrows.Count / 4, "short arrows are the exception");
        Assert.True(arrows.Count(a => a.Length >= 20) >= 30, "plenty of really long arrows");
        Assert.True(arrows.Count(a => !a.IsStraight) > arrows.Count / 2, "most arrows twist and turn");
    }

    [Fact]
    public void WallHugging_KeepsBoardsFullAndSolvable()
    {
        for (var seed = 0; seed < 5; seed++)
        {
            var arrows = new LevelGenerator(new Random(seed)).Generate(new GeneratorSettings
            {
                Width = 20, Height = 14, MinLength = 4, MaxLength = 24, BendChance = 0.3, HugWalls = true,
            });

            Assert.Equal(20 * 14, arrows.Sum(a => a.Length));
            Assert.True(new GreedySolver().Solve(Board.Create(20, 14, arrows)).IsSolvable, $"seed {seed}");
        }
    }

    [Fact]
    public void Generate_SameSeedSameBoard_DifferentSeedDifferentBoard()
    {
        var a = ChallengeGenerator.Generate(5);
        var b = ChallengeGenerator.Generate(5);
        var c = ChallengeGenerator.Generate(6);

        Assert.Equal(a.Arrows, b.Arrows);
        Assert.NotEqual(Challenge.EncodeLayout(a.Arrows), Challenge.EncodeLayout(c.Arrows));
    }

    // ---------- Layout storage ----------

    [Fact]
    public void Layout_RoundTripsBentArrowsWithStableIds()
    {
        var arrows = ChallengeGenerator.Generate(3).Arrows;

        var decoded = Challenge.DecodeLayout(Challenge.EncodeLayout(arrows));

        Assert.Equal(arrows.Count, decoded.Count);
        Assert.Equal(Enumerable.Range(1, arrows.Count), decoded.Select(a => a.Id));
        Assert.All(arrows.Zip(decoded), pair =>
        {
            Assert.Equal(pair.First.Direction, pair.Second.Direction);
            Assert.Equal(pair.First.Cells, pair.Second.Cells);
        });
    }

    [Theory]
    [InlineData("9:1,1")]
    [InlineData("1:1,1|x")]
    [InlineData("1:a,b")]
    public void Layout_RejectsCorruptData(string layout)
    {
        Assert.Throws<FormatException>(() => Challenge.DecodeLayout(layout));
    }

    // ---------- Service ----------

    [Fact]
    public async Task Create_StoresARandomBigBoardOwnedByThePlayer()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);

        var id = await CreateService().CreateAsync(TestDb.PlayerId, ChallengeKind.Normal);

        var stored = await _db.Challenges.SingleAsync();
        Assert.Equal(id, stored.Id);
        Assert.Equal(TestDb.PlayerId, stored.OwnerId);
        Assert.Equal(ChallengeKind.Normal, stored.Kind);
        Assert.True(stored.ArrowCount >= ChallengeGenerator.MinArrows(ChallengeKind.Normal));
        Assert.Equal(stored.ArrowCount, stored.GetArrows().Count);
        Assert.False(stored.IsCompleted);
    }

    [Fact]
    public async Task OtherPlayers_CannotSeeOrPlaySomeoneElsesChallenge()
    {
        var challenge = await SeedAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetBoardAsync(challenge.Id, TestDb.OtherPlayerId));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.SubmitCompletionAsync(
            challenge.Id, new CompletionRequest { Taps = TestDb.ChallengeWinningTaps.ToList() }, TestDb.OtherPlayerId));
    }

    [Fact]
    public async Task GetBoard_ReturnsTheStoredLayout()
    {
        var challenge = await SeedAsync();

        var board = await CreateService().GetBoardAsync(challenge.Id, TestDb.PlayerId);

        Assert.Equal((5, 3, 3), (board.Width, board.Height, board.MaxLives));
        Assert.Equal([1, 2, 3], board.Arrows.Select(a => a.Id));
    }

    [Fact]
    public async Task SubmitCompletion_ValidReplay_RecordsTheWin()
    {
        var challenge = await SeedAsync();

        var result = await CreateService().SubmitCompletionAsync(
            challenge.Id, new CompletionRequest { Taps = TestDb.ChallengeWinningTaps.ToList() }, TestDb.PlayerId);

        Assert.Equal((3, 0, true), (result.Stars, result.Mistakes, result.IsFirstCompletion));
        var stored = await _db.Challenges.SingleAsync();
        Assert.True(stored.IsCompleted);
        Assert.Equal(3, stored.Stars);
        _analytics.Verify(a => a.Track("game_completed", TestDb.PlayerId, It.IsAny<IReadOnlyDictionary<string, object?>>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCompletion_WithACollision_CostsAStar()
    {
        var challenge = await SeedAsync();
        var taps = new List<int> { 1 }.Concat(TestDb.ChallengeWinningTaps).ToList(); // arrow 1 is blocked at first

        var result = await CreateService().SubmitCompletionAsync(challenge.Id, new CompletionRequest { Taps = taps }, TestDb.PlayerId);

        Assert.Equal((2, 1), (result.Stars, result.Mistakes));
    }

    [Theory]
    [InlineData(new[] { 999 })]  // tampered: not an arrow on this board
    [InlineData(new[] { 3, 2 })] // board not cleared
    public async Task SubmitCompletion_InvalidReplay_IsRejectedAndNothingIsSaved(int[] taps)
    {
        var challenge = await SeedAsync();

        await Assert.ThrowsAsync<InvalidGameStateException>(() => CreateService().SubmitCompletionAsync(
            challenge.Id, new CompletionRequest { Taps = taps }, TestDb.PlayerId));

        Assert.False((await _db.Challenges.AsNoTracking().SingleAsync()).IsCompleted);
    }

    // ---------- Points ----------

    [Theory]
    [InlineData(ChallengeKind.Easy, 1)]
    [InlineData(ChallengeKind.Normal, 4)]
    [InlineData(ChallengeKind.Hard, 10)]
    [InlineData(ChallengeKind.Challenge, 20)]
    public void Points_AreAFixedAmountPerKind(ChallengeKind kind, int expected)
    {
        Assert.Equal(expected, Challenge.PointsFor(kind));
        Assert.Throws<ArgumentOutOfRangeException>(() => Challenge.PointsFor((ChallengeKind)9));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)] // crashes don't change the points
    public void Points_WinningABoardScoresOnce_ReplaysEarnNothingMore(int mistakes)
    {
        var challenge = TestDb.QueueChallenge(TestDb.PlayerId, ChallengeKind.Normal);

        var first = challenge.RecordWin(mistakes, DateTime.UtcNow);
        Assert.Equal((4, 4), (first.Points, first.PointsGained));

        var replay = challenge.RecordWin(0, DateTime.UtcNow);
        Assert.Equal((4, 0), (replay.Points, replay.PointsGained));
        Assert.Equal(4, challenge.Points); // no farming by replaying
    }

    [Fact]
    public void Points_NewBestStillMeansFewerCollisions()
    {
        var challenge = TestDb.QueueChallenge(TestDb.PlayerId);

        Assert.True(challenge.RecordWin(2, DateTime.UtcNow).IsNewBest);
        Assert.True(challenge.RecordWin(0, DateTime.UtcNow).IsNewBest);
        Assert.False(challenge.RecordWin(1, DateTime.UtcNow).IsNewBest);
    }

    [Fact]
    public async Task Points_HintsAreCountedByTheServer_ButDoNotCostPoints()
    {
        var challenge = await SeedAsync(); // an Easy board
        var service = CreateService();

        await service.StartAttemptAsync(challenge.Id, TestDb.PlayerId);
        await service.GetHintAsync(challenge.Id, [1, 2, 3], TestDb.PlayerId);
        await service.GetHintAsync(challenge.Id, [1, 2, 3], TestDb.PlayerId);

        var result = await service.SubmitCompletionAsync(
            challenge.Id, new CompletionRequest { Taps = TestDb.ChallengeWinningTaps.ToList(), HintsUsed = 0 }, TestDb.PlayerId);

        Assert.Equal(2, result.HintsUsed); // our own count, not the "0" the browser sent
        Assert.Equal((1, 1, 1), (result.Points, result.PointsGained, result.TotalPoints));

        await service.StartAttemptAsync(challenge.Id, TestDb.PlayerId);
        Assert.Equal(0, (await _db.Challenges.AsNoTracking().SingleAsync()).HintsThisAttempt);
    }

    [Fact]
    public async Task Hint_PointsAtTheOnlyFreeArrow()
    {
        var challenge = await SeedAsync();

        var hint = await CreateService().GetHintAsync(challenge.Id, [1, 2, 3], TestDb.PlayerId);

        Assert.Equal(3, hint.ArrowId);
    }

    [Fact]
    public async Task Summary_CountsPlayedWonAndPointsAtTheLatestUnfinished()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        await TestDb.AddChallengeAsync(_db, TestDb.PlayerId, wonWithMistakes: 1);
        var unfinished = await TestDb.AddChallengeAsync(_db, TestDb.PlayerId);

        var summary = await CreateService().GetSummaryAsync(TestDb.PlayerId);

        // One won Easy board = 1 point (crashes don't matter).
        Assert.Equal(new ChallengeSummary(Played: 2, Won: 1, TotalStars: 2, TotalPoints: 1, UnfinishedId: unfinished.Id), summary);
    }

    // ---------- API ----------

    [Fact]
    public async Task Api_GetChallenge_UsesTheAuthenticatedUser()
    {
        var service = new Mock<IChallengeService>();
        var dto = new ChallengeBoardDto(4, 5, 3, 3, []);
        service.Setup(s => s.GetBoardAsync(4, "u", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new ChallengesApiController(service.Object).WithUser("u").GetChallenge(4, CancellationToken.None);

        Assert.Same(dto, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private async Task<Challenge> SeedAsync()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        await TestDb.AddUserAsync(_db, TestDb.OtherPlayerId);
        return await TestDb.AddChallengeAsync(_db, TestDb.PlayerId);
    }
}
