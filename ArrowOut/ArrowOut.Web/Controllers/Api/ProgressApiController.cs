using ArrowOut.Game.Generation;
using ArrowOut.Services.Leaderboard;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers.Api;

[Route("api")]
public class ProgressApiController(ILevelService levelService, ILeaderboardService leaderboardService) : ApiControllerBase
{
    // GET /api/progress: the player's level stats.
    [HttpGet("progress")]
    [ProducesResponseType<ProgressSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProgressSummary>> GetProgress(CancellationToken cancellationToken) =>
        Ok(await levelService.GetProgressSummaryAsync(CurrentUserId, cancellationToken));

    // GET /api/leaderboard?kind=Easy|Medium|Hard&page=&pageSize=: public, no login needed.
    [HttpGet("leaderboard")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<LeaderboardEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LeaderboardEntry>>> GetLeaderboard(
        [FromQuery] ChallengeKind kind = ChallengeKind.Easy, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Enum.IsDefined(kind)
            ? Ok(await leaderboardService.GetPageAsync(kind, page, pageSize, cancellationToken))
            : BadRequest();
}
