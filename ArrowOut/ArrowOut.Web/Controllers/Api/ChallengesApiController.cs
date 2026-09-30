using ArrowOut.Services.Challenges;
using ArrowOut.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers.Api;

// The player's own boards and the moves on them.
[Route("api/challenges")]
public class ChallengesApiController(IChallengeService challengeService) : ApiControllerBase
{
    // GET /api/challenges/{id}: the board. Only the owner gets it, everyone else gets a 404.
    [HttpGet("{id:int:min(1)}")]
    [ProducesResponseType<ChallengeBoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChallengeBoardDto>> GetChallenge(int id, CancellationToken cancellationToken) =>
        Ok(await challengeService.GetBoardAsync(id, CurrentUserId, cancellationToken));

    // POST /api/challenges/{id}/attempts: the player started or restarted.
    [HttpPost("{id:int:min(1)}/attempts")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StartAttempt(int id, CancellationToken cancellationToken)
    {
        await challengeService.StartAttemptAsync(id, CurrentUserId, cancellationToken);
        return NoContent();
    }

    // POST /api/challenges/{id}/hint: a safe arrow to tap next.
    [HttpPost("{id:int:min(1)}/hint")]
    [ProducesResponseType<HintResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HintResult>> GetHint(int id, [FromBody] BoardStateRequest request, CancellationToken cancellationToken) =>
        Ok(await challengeService.GetHintAsync(id, request.RemainingArrowIds, CurrentUserId, cancellationToken));

    // POST /api/challenges/{id}/moves: what a tap would do. Nothing gets saved.
    [HttpPost("{id:int:min(1)}/moves")]
    [ProducesResponseType<MoveCheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MoveCheckResult>> CheckMove(int id, [FromBody] MoveRequest request, CancellationToken cancellationToken) =>
        Ok(await challengeService.CheckMoveAsync(id, request.RemainingArrowIds, request.ArrowId, CurrentUserId, cancellationToken));

    // POST /api/challenges/{id}/completion: the list of taps. The server replays it before saving the win.
    [HttpPost("{id:int:min(1)}/completion")]
    [ProducesResponseType<ChallengeCompletionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ChallengeCompletionResult>> Complete(int id, [FromBody] CompletionRequest request, CancellationToken cancellationToken) =>
        Ok(await challengeService.SubmitCompletionAsync(id, request, CurrentUserId, cancellationToken));
}
