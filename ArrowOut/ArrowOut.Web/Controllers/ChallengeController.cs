using ArrowOut.Game.Generation;
using ArrowOut.Services.Challenges;
using ArrowOut.Web.Infrastructure;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

// Every "Start" makes a new random board of the kind you picked.
[Authorize]
[Route("challenge")]
public class ChallengeController(IChallengeService challengeService) : Controller
{
    // POST /challenge/new: makes a new board. It's a POST because it creates data (and gets CSRF checks).
    [HttpPost("new")]
    public async Task<IActionResult> New([FromForm] ChallengeKind kind = ChallengeKind.Hard, CancellationToken cancellationToken = default)
    {
        // Text that isn't a kind makes ModelState invalid, and numbers outside the enum still bind but
        // aren't defined. Either way someone messed with the form, so don't just build some other board.
        if (!ModelState.IsValid || !Enum.IsDefined(kind))
        {
            return BadRequest();
        }

        var id = await challengeService.CreateAsync(User.GetRequiredUserId(), kind, cancellationToken);
        return RedirectToAction(nameof(Play), new { id });
    }

    // GET /challenge/{id}: the game page. The browser loads the board from the API afterwards.
    [HttpGet("{id:int:min(1)}")]
    public async Task<IActionResult> Play(int id, CancellationToken cancellationToken)
    {
        var info = await challengeService.GetInfoAsync(id, User.GetRequiredUserId(), cancellationToken);
        return View(new ChallengeViewModel { Challenge = info });
    }
}
