using ArrowOut.Data;
using ArrowOut.Data.Models;
using ArrowOut.Game;
using ArrowOut.Game.Replay;
using ArrowOut.Game.Solving;
using ArrowOut.Services.Analytics;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArrowOut.Services.Gameplay;

public sealed class GameService(
    ApplicationDbContext dbContext,
    ILevelAccessGuard accessGuard,
    IHintProvider hintProvider,
    IAnalyticsTracker analytics,
    TimeProvider timeProvider,
    ILogger<GameService> logger) : IGameService
{
    public async Task StartAttemptAsync(int levelId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var level = await LoadPlayableLevelAsync(levelId, userId, isAdmin, includeArrows: false, cancellationToken);

        var progress = await GetOrCreateProgressAsync(userId, level.Id, cancellationToken);
        progress.RecordAttempt(timeProvider.GetUtcNow().UtcDateTime);
        await SaveProgressAsync(cancellationToken);

        analytics.Track("level_started", userId, new Dictionary<string, object?>
        {
            ["level_number"] = level.Number,
            ["difficulty"] = level.Difficulty.ToString(),
            ["attempt"] = progress.Attempts,
        });
    }

    public async Task<HintResult> GetHintAsync(
        int levelId, IEnumerable<int> remainingArrowIds, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var board = await BuildClientBoardAsync(levelId, remainingArrowIds, userId, isAdmin, cancellationToken);
        return new HintResult(hintProvider.GetHint(board));
    }

    public async Task<MoveCheckResult> CheckMoveAsync(
        int levelId, IEnumerable<int> remainingArrowIds, int arrowId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var board = await BuildClientBoardAsync(levelId, remainingArrowIds, userId, isAdmin, cancellationToken);

        if (!board.Contains(arrowId))
        {
            throw new InvalidGameStateException($"Arrow #{arrowId} is not on the board.");
        }

        return board.Peek(arrowId) switch
        {
            CollisionMoveResult collision => new MoveCheckResult(arrowId, false, collision.Distance, collision.BlockerArrowId),
            var exit => new MoveCheckResult(arrowId, true, exit.Distance, null),
        };
    }

    public async Task<CompletionResult> SubmitCompletionAsync(
        int levelId, CompletionRequest request, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var level = await LoadPlayableLevelAsync(levelId, userId, isAdmin, includeArrows: true, cancellationToken);
        var replay = GameReplayer.Replay(level.ToBoard(), request.Taps.ToList(), level.MaxLives);

        if (!replay.IsValid)
        {
            logger.LogWarning(
                "Rejected tampered or corrupt replay for level {LevelId} by {UserId}: {Error}", levelId, userId, replay.Error);
            throw new InvalidGameStateException(replay.Error ?? "The move sequence is not valid.");
        }

        if (!replay.IsWin)
        {
            throw new InvalidGameStateException(replay.LivesExhausted
                ? "All lives were lost; the level was not completed."
                : "The board is not cleared yet.");
        }

        var progress = await GetOrCreateProgressAsync(userId, level.Id, cancellationToken);
        var isFirstCompletion = !progress.IsCompleted;
        var isNewBest = progress.RecordWin(replay.Mistakes, timeProvider.GetUtcNow().UtcDateTime);
        await SaveProgressAsync(cancellationToken);

        var nextNumber = await dbContext.Levels
            .Where(l => l.IsPublished && l.Number > level.Number)
            .OrderBy(l => l.Number)
            .Select(l => (int?)l.Number)
            .FirstOrDefaultAsync(cancellationToken);

        analytics.Track("level_completed", userId, new Dictionary<string, object?>
        {
            ["level_number"] = level.Number,
            ["difficulty"] = level.Difficulty.ToString(),
            ["mistakes"] = replay.Mistakes,
            ["hints_used"] = request.HintsUsed,
            ["first_completion"] = isFirstCompletion,
        });

        return new CompletionResult(
            level.Id,
            PlayerProgress.CalculateStars(replay.Mistakes),
            replay.Mistakes,
            isNewBest,
            isFirstCompletion,
            nextNumber);
    }

    private async Task<Board> BuildClientBoardAsync(
        int levelId, IEnumerable<int> remainingArrowIds, string userId, bool isAdmin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remainingArrowIds);

        var level = await LoadPlayableLevelAsync(levelId, userId, isAdmin, includeArrows: true, cancellationToken);

        try
        {
            return level.ToBoard().WithOnly(remainingArrowIds);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidGameStateException(ex.Message);
        }
    }

    private async Task<Level> LoadPlayableLevelAsync(
        int levelId, string userId, bool isAdmin, bool includeArrows, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var query = dbContext.Levels.AsNoTracking();
        if (includeArrows)
        {
            query = query.Include(l => l.Arrows);
        }

        var level = await query.FirstOrDefaultAsync(l => l.Id == levelId, cancellationToken)
            ?? throw new EntityNotFoundException("Level", levelId);

        await accessGuard.EnsureCanPlayAsync(level.Id, level.Number, level.IsPublished, userId, isAdmin, cancellationToken);
        return level;
    }

    private async Task<PlayerProgress> GetOrCreateProgressAsync(string userId, int levelId, CancellationToken cancellationToken)
    {
        var progress = await dbContext.PlayerProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId, cancellationToken);

        if (progress is null)
        {
            progress = new PlayerProgress(userId, levelId);
            dbContext.PlayerProgress.Add(progress);
        }

        return progress;
    }

    private async Task SaveProgressAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // If two tabs finish at the same time they both try to insert the same (UserId, LevelId) row.
            // The first one wins, and this one returns a conflict instead of blowing up with a 500.
            logger.LogWarning(ex, "Concurrent progress update detected");
            throw new OperationNotAllowedException("Your progress was updated from another session. Please retry.");
        }
    }
}
