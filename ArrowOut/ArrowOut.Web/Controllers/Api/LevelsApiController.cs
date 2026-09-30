using ArrowOut.Services.Gameplay;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers.Api;

// The admin-made levels and the moves on them.
[Route("api/levels")]
public class LevelsApiController(ILevelService levelService, IGameService gameService) : ApiControllerBase
{
    // GET /api/levels?search=&difficulty=&status=&page=&pageSize=
    [HttpGet]
    [ProducesResponseType<PagedResult<LevelListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LevelListItem>>> GetLevels([FromQuery] LevelQuery query, CancellationToken cancellationToken) =>
        Ok(await levelService.GetLevelsAsync(query, CurrentUserId, IsAdministrator, cancellationToken));

    // GET /api/levels/{id}: the board, only if the level is unlocked.
    [HttpGet("{id:int:min(1)}")]
    [ProducesResponseType<LevelBoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LevelBoardDto>> GetLevel(int id, CancellationToken cancellationToken) =>
        Ok(await levelService.GetBoardAsync(id, CurrentUserId, IsAdministrator, cancellationToken));

    // POST /api/levels/{id}/attempts: the player started or restarted (counts plays, sends analytics).
    [HttpPost("{id:int:min(1)}/attempts")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StartAttempt(int id, CancellationToken cancellationToken)
    {
        await gameService.StartAttemptAsync(id, CurrentUserId, IsAdministrator, cancellationToken);
        return NoContent();
    }

    // POST /api/levels/{id}/hint: a safe arrow to tap next.
    [HttpPost("{id:int:min(1)}/hint")]
    [ProducesResponseType<HintResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HintResult>> GetHint(int id, [FromBody] BoardStateRequest request, CancellationToken cancellationToken) =>
        Ok(await gameService.GetHintAsync(id, request.RemainingArrowIds, CurrentUserId, IsAdministrator, cancellationToken));

    // POST /api/levels/{id}/moves: what a tap would do. Nothing gets saved.
    [HttpPost("{id:int:min(1)}/moves")]
    [ProducesResponseType<MoveCheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MoveCheckResult>> CheckMove(int id, [FromBody] MoveRequest request, CancellationToken cancellationToken) =>
        Ok(await gameService.CheckMoveAsync(id, request.RemainingArrowIds, request.ArrowId, CurrentUserId, IsAdministrator, cancellationToken));

    // POST /api/levels/{id}/completion: the list of taps. The server replays it before saving the win.
    [HttpPost("{id:int:min(1)}/completion")]
    [ProducesResponseType<CompletionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CompletionResult>> Complete(int id, [FromBody] CompletionRequest request, CancellationToken cancellationToken) =>
        Ok(await gameService.SubmitCompletionAsync(id, request, CurrentUserId, IsAdministrator, cancellationToken));
}
