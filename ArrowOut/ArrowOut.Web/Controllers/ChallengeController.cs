using ArrowOut.Game.Generation;
using ArrowOut.Services.Challenges;
using ArrowOut.Web.Infrastructure;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

// Every "Start game" makes a new random board, and the difficulty is a surprise too.
// "Challenge game" is a separate mode with much bigger boards.
[Authorize]
[Route("game")]
public class ChallengeController(IChallengeService challengeService) : Controller
{
    // What "Start game" picks from. The Challenge game has its own button, so it's not in here.
    internal static readonly ChallengeKind[] RandomKinds = [ChallengeKind.Easy, ChallengeKind.Normal, ChallengeKind.Hard];

    // POST /game/new: makes a new board. It's a POST because it creates data (and gets CSRF checks).
    // "Start game" sends no kind and we pick Easy, Normal or Hard at random. "Challenge game" sends kind=Challenge.
    [HttpPost("new")]
    public async Task<IActionResult> New([FromForm] ChallengeKind? kind = null, CancellationToken cancellationToken = default)
    {
        // Text that isn't a kind makes ModelState invalid, and numbers outside the enum still bind but
        // aren't defined. Either way someone messed with the form, so don't just build some other board.
        if (!ModelState.IsValid || (kind is { } requested && !Enum.IsDefined(requested)))
        {
            return BadRequest();
        }

        var chosen = kind ?? RandomKinds[Random.Shared.Next(RandomKinds.Length)];
        var id = await challengeService.CreateAsync(User.GetRequiredUserId(), chosen, cancellationToken);
        return RedirectToAction(nameof(Play), new { id });
    }

    // GET /game/{id}: the game page. The browser loads the board from the API afterwards.
    [HttpGet("{id:int:min(1)}")]
    public async Task<IActionResult> Play(int id, CancellationToken cancellationToken)
    {
        var info = await challengeService.GetInfoAsync(id, User.GetRequiredUserId(), cancellationToken);
        return View(new ChallengeViewModel { Challenge = info });
    }
}
