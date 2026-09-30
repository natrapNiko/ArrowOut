using ArrowOut.Data;
using ArrowOut.Game.Solving;
using ArrowOut.Services.Analytics;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Gameplay;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using ArrowOut.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArrowOut.Tests.Services;

public class GameServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly Mock<IAnalyticsTracker> _analytics = new();

    private GameService CreateService() => new(
        _db,
        new LevelAccessGuard(_db),
        new UnblockingHintProvider(),
        _analytics.Object,
        TimeProvider.System,
        NullLogger<GameService>.Instance);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SubmitCompletion_ValidReplay_RecordsProgressAndReturnsStars()
    {
        var (first, second) = await SeedTwoLevelsAsync();

        var result = await CreateService().SubmitCompletionAsync(
            first.Id, new CompletionRequest { Taps = TestDb.WinningTaps(first).ToList() }, TestDb.PlayerId, isAdmin: false);

        Assert.Equal(3, result.Stars);
        Assert.Equal(0, result.Mistakes);
        Assert.True(result.IsFirstCompletion);
        Assert.Equal(second.Number, result.NextLevelNumber);

        var progress = await _db.PlayerProgress.SingleAsync();
        Assert.True(progress.IsCompleted);
        Assert.Equal(3, progress.Stars);

        _analytics.Verify(a => a.Track("level_completed", TestDb.PlayerId, It.IsAny<IReadOnlyDictionary<string, object?>>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCompletion_WithCollisions_CountsMistakes()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var winning = TestDb.WinningTaps(first);
        var taps = new List<int> { winning[2] }.Concat(winning).ToList(); // tapping the back arrow first = crash

        var result = await CreateService().SubmitCompletionAsync(first.Id, new CompletionRequest { Taps = taps }, TestDb.PlayerId, false);

        Assert.Equal(1, result.Mistakes);
        Assert.Equal(2, result.Stars);
    }

    [Fact]
    public async Task SubmitCompletion_TamperedSequence_IsRejectedAndNothingIsSaved()
    {
        var (first, _) = await SeedTwoLevelsAsync();

        await Assert.ThrowsAsync<InvalidGameStateException>(() => CreateService().SubmitCompletionAsync(
            first.Id, new CompletionRequest { Taps = [999_999] }, TestDb.PlayerId, false));

        Assert.Empty(_db.PlayerProgress);
    }

    [Fact]
    public async Task SubmitCompletion_BoardNotCleared_IsRejected()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var winning = TestDb.WinningTaps(first);

        await Assert.ThrowsAsync<InvalidGameStateException>(() => CreateService().SubmitCompletionAsync(
            first.Id, new CompletionRequest { Taps = [winning[0]] }, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task SubmitCompletion_LivesExhausted_IsRejected()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var back = TestDb.WinningTaps(first)[2];

        await Assert.ThrowsAsync<InvalidGameStateException>(() => CreateService().SubmitCompletionAsync(
            first.Id, new CompletionRequest { Taps = [back, back, back] }, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task SubmitCompletion_LockedLevel_IsForbidden()
    {
        var (_, second) = await SeedTwoLevelsAsync();

        await Assert.ThrowsAsync<LevelLockedException>(() => CreateService().SubmitCompletionAsync(
            second.Id, new CompletionRequest { Taps = TestDb.WinningTaps(second).ToList() }, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task SubmitCompletion_UnpublishedLevel_IsHiddenFromPlayers()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        var draft = (await TestDb.AddLevelsAsync(_db, TestDb.QueueLevel(1, published: false)))[0];

        await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService().SubmitCompletionAsync(
            draft.Id, new CompletionRequest { Taps = TestDb.WinningTaps(draft).ToList() }, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task SubmitCompletion_AdminMayPlayLockedAndDraftLevels()
    {
        var (_, second) = await SeedTwoLevelsAsync();

        var result = await CreateService().SubmitCompletionAsync(
            second.Id, new CompletionRequest { Taps = TestDb.WinningTaps(second).ToList() }, TestDb.PlayerId, isAdmin: true);

        Assert.Equal(3, result.Stars);
    }

    [Fact]
    public async Task SubmitCompletion_ReplayWorseRun_KeepsBest()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var service = CreateService();
        var winning = TestDb.WinningTaps(first).ToList();

        await service.SubmitCompletionAsync(first.Id, new CompletionRequest { Taps = winning }, TestDb.PlayerId, false);
        var second = await service.SubmitCompletionAsync(
            first.Id, new CompletionRequest { Taps = new List<int> { winning[2] }.Concat(winning).ToList() }, TestDb.PlayerId, false);

        Assert.False(second.IsNewBest);
        Assert.False(second.IsFirstCompletion);
        Assert.Equal(3, (await _db.PlayerProgress.SingleAsync()).Stars);
    }

    [Fact]
    public async Task GetHint_ReturnsTheFrontArrow()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var ids = first.Arrows.Select(a => a.Id).ToList();

        var hint = await CreateService().GetHintAsync(first.Id, ids, TestDb.PlayerId, false);

        Assert.Equal(TestDb.WinningTaps(first)[0], hint.ArrowId);
    }

    [Fact]
    public async Task GetHint_ForeignArrowIds_AreRejected()
    {
        var (first, second) = await SeedTwoLevelsAsync();
        var foreign = second.Arrows.Select(a => a.Id).ToList();

        await Assert.ThrowsAsync<InvalidGameStateException>(() =>
            CreateService().GetHintAsync(first.Id, foreign, TestDb.PlayerId, false));
    }

    [Fact]
    public async Task CheckMove_ReportsCollisionWithBlocker()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var winning = TestDb.WinningTaps(first);

        var result = await CreateService().CheckMoveAsync(
            first.Id, first.Arrows.Select(a => a.Id), winning[2], TestDb.PlayerId, false);

        Assert.False(result.IsSuccess);
        Assert.Equal(winning[1], result.BlockerArrowId);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public async Task CheckMove_ArrowNotInState_IsRejected()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var winning = TestDb.WinningTaps(first);

        await Assert.ThrowsAsync<InvalidGameStateException>(() => CreateService().CheckMoveAsync(
            first.Id, [winning[1], winning[2]], winning[0], TestDb.PlayerId, false));
    }

    [Fact]
    public async Task StartAttempt_IncrementsAttempts()
    {
        var (first, _) = await SeedTwoLevelsAsync();
        var service = CreateService();

        await service.StartAttemptAsync(first.Id, TestDb.PlayerId, false);
        await service.StartAttemptAsync(first.Id, TestDb.PlayerId, false);

        Assert.Equal(2, (await _db.PlayerProgress.SingleAsync()).Attempts);
    }

    [Fact]
    public async Task UnknownLevel_Throws404()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService().StartAttemptAsync(12345, TestDb.PlayerId, false));
    }

    private async Task<(ArrowOut.Data.Models.Level First, ArrowOut.Data.Models.Level Second)> SeedTwoLevelsAsync()
    {
        await TestDb.AddUserAsync(_db, TestDb.PlayerId);
        var levels = await TestDb.AddLevelsAsync(_db, TestDb.QueueLevel(1), TestDb.QueueLevel(2));
        return (levels[0], levels[1]);
    }
}
