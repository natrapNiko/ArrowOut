using ArrowOut.Data;
using ArrowOut.Data.Models;
using ArrowOut.Game;
using ArrowOut.Game.Generation;
using ArrowOut.Game.Replay;
using ArrowOut.Game.Solving;
using ArrowOut.Services.Analytics;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArrowOut.Services.Challenges;

public interface IChallengeService
{
    // Makes a new random board of this kind for the player and returns its id.
    Task<int> CreateAsync(string userId, ChallengeKind kind, CancellationToken cancellationToken = default);

    Task<ChallengeInfo> GetInfoAsync(int challengeId, string userId, CancellationToken cancellationToken = default);

    Task<ChallengeBoardDto> GetBoardAsync(int challengeId, string userId, CancellationToken cancellationToken = default);

    Task StartAttemptAsync(int challengeId, string userId, CancellationToken cancellationToken = default);

    Task<HintResult> GetHintAsync(int challengeId, IEnumerable<int> remainingArrowIds, string userId, CancellationToken cancellationToken = default);

    Task<MoveCheckResult> CheckMoveAsync(int challengeId, IEnumerable<int> remainingArrowIds, int arrowId, string userId, CancellationToken cancellationToken = default);

    // Replays all the taps on the server before saving a win, so nobody can fake one.
    Task<ChallengeCompletionResult> SubmitCompletionAsync(int challengeId, CompletionRequest request, string userId, CancellationToken cancellationToken = default);

    Task<ChallengeSummary> GetSummaryAsync(string userId, CancellationToken cancellationToken = default);
}

// Games are the only mode now. A board belongs to whoever started it. Anyone else gets
// a 404, same as if the board didn't exist, so you can't find other boards by guessing ids.
public sealed class ChallengeService(
    ApplicationDbContext dbContext,
    IHintProvider hintProvider,
    IAnalyticsTracker analytics,
    TimeProvider timeProvider,
    ILogger<ChallengeService> logger) : IChallengeService
{
    public async Task<int> CreateAsync(string userId, ChallengeKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var blueprint = ChallengeGenerator.Generate(Random.Shared.Next(), kind);
        var challenge = new Challenge(userId, blueprint.Kind, blueprint.Seed, blueprint.Width, blueprint.Height, blueprint.MaxLives, blueprint.Arrows);

        dbContext.Challenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        analytics.Track("game_created", userId, new Dictionary<string, object?>
        {
            ["kind"] = kind.ToString(),
            ["arrows"] = challenge.ArrowCount,
            ["size"] = challenge.Width,
        });

        return challenge.Id;
    }

    public async Task<ChallengeInfo> GetInfoAsync(int challengeId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return await dbContext.Challenges
            .AsNoTracking()
            .Where(c => c.Id == challengeId && c.OwnerId == userId)
            .Select(c => new ChallengeInfo(c.Id, c.Kind, c.Width, c.Height, c.ArrowCount, c.MaxLives, c.IsCompleted, c.Stars))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Game", challengeId);
    }

    public async Task<ChallengeBoardDto> GetBoardAsync(int challengeId, string userId, CancellationToken cancellationToken = default)
    {
        var challenge = await LoadAsync(challengeId, userId, tracking: false, cancellationToken);
        var arrows = challenge.GetArrows().Select(ArrowDto.FromPiece).ToList();
        return new ChallengeBoardDto(challenge.Id, challenge.Width, challenge.Height, challenge.MaxLives, arrows);
    }

    public async Task StartAttemptAsync(int challengeId, string userId, CancellationToken cancellationToken = default)
    {
        var challenge = await LoadAsync(challengeId, userId, tracking: true, cancellationToken);
        challenge.RecordAttempt(timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<HintResult> GetHintAsync(
        int challengeId, IEnumerable<int> remainingArrowIds, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remainingArrowIds);

        var challenge = await LoadAsync(challengeId, userId, tracking: true, cancellationToken);
        var hint = hintProvider.GetHint(ToClientBoard(challenge, remainingArrowIds));

        // We count hints here for the stats. The number the browser sends isn't trusted.
        if (hint is not null)
        {
            challenge.RecordHint();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new HintResult(hint);
    }

    public async Task<MoveCheckResult> CheckMoveAsync(
        int challengeId, IEnumerable<int> remainingArrowIds, int arrowId, string userId, CancellationToken cancellationToken = default)
    {
        var board = await BuildClientBoardAsync(challengeId, remainingArrowIds, userId, cancellationToken);

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

    public async Task<ChallengeCompletionResult> SubmitCompletionAsync(
        int challengeId, CompletionRequest request, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var challenge = await LoadAsync(challengeId, userId, tracking: true, cancellationToken);
        var replay = GameReplayer.Replay(challenge.ToBoard(), request.Taps.ToList(), challenge.MaxLives);

        if (!replay.IsValid)
        {
            logger.LogWarning(
                "Rejected tampered or corrupt replay for game {GameId} by {UserId}: {Error}", challengeId, userId, replay.Error);
            throw new InvalidGameStateException(replay.Error ?? "The move sequence is not valid.");
        }

        if (!replay.IsWin)
        {
            throw new InvalidGameStateException(replay.LivesExhausted
                ? "All lives were lost; the game was not completed."
                : "The board is not cleared yet.");
        }

        var isFirstCompletion = !challenge.IsCompleted;
        var hintsUsed = challenge.HintsThisAttempt;
        var win = challenge.RecordWin(replay.Mistakes, timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);

        analytics.Track("game_completed", userId, new Dictionary<string, object?>
        {
            ["kind"] = challenge.Kind.ToString(),
            ["arrows"] = challenge.ArrowCount,
            ["mistakes"] = replay.Mistakes,
            ["hints_used"] = hintsUsed,
            ["points"] = win.Points,
            ["first_completion"] = isFirstCompletion,
        });

        var totalPoints = await dbContext.Challenges
            .Where(c => c.OwnerId == userId && c.IsCompleted)
            .SumAsync(c => c.Points, cancellationToken);

        return new ChallengeCompletionResult(
            challenge.Id,
            PlayerProgress.CalculateStars(replay.Mistakes),
            replay.Mistakes,
            win.IsNewBest,
            isFirstCompletion,
            win.Points,
            win.PointsGained,
            hintsUsed,
            totalPoints);
    }

    public async Task<ChallengeSummary> GetSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var mine = dbContext.Challenges.AsNoTracking().Where(c => c.OwnerId == userId);

        var played = await mine.CountAsync(cancellationToken);
        var won = await mine.CountAsync(c => c.IsCompleted, cancellationToken);
        var stars = await mine.SumAsync(c => c.Stars, cancellationToken);
        var points = await mine.SumAsync(c => c.Points, cancellationToken);
        var unfinished = await mine
            .Where(c => !c.IsCompleted)
            .OrderByDescending(c => c.CreatedOn)
            .ThenByDescending(c => c.Id)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new ChallengeSummary(played, won, stars, points, unfinished);
    }

    private async Task<Board> BuildClientBoardAsync(
        int challengeId, IEnumerable<int> remainingArrowIds, string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remainingArrowIds);

        var challenge = await LoadAsync(challengeId, userId, tracking: false, cancellationToken);
        return ToClientBoard(challenge, remainingArrowIds);
    }

    // The board the way the browser says it looks right now (only these arrows left).
    private static Board ToClientBoard(Challenge challenge, IEnumerable<int> remainingArrowIds)
    {
        try
        {
            return challenge.ToBoard().WithOnly(remainingArrowIds);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidGameStateException(ex.Message);
        }
    }

    private async Task<Challenge> LoadAsync(int challengeId, string userId, bool tracking, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var query = tracking ? dbContext.Challenges : dbContext.Challenges.AsNoTracking();
        return await query.FirstOrDefaultAsync(c => c.Id == challengeId && c.OwnerId == userId, cancellationToken)
            ?? throw new EntityNotFoundException("Game", challengeId);
    }
}
