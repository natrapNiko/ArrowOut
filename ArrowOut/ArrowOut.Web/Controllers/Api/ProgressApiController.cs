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

    // GET /api/leaderboard?page=&pageSize=: the one ranking for all games. Public, no login needed.
    [HttpGet("leaderboard")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<LeaderboardEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LeaderboardEntry>>> GetLeaderboard(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await leaderboardService.GetPageAsync(page, pageSize, cancellationToken));
}
