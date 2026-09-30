using System.Security.Claims;
using ArrowOut.Game.Generation;
using ArrowOut.Services.Challenges;
using ArrowOut.Services.Leaderboard;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

// One public ranking per kind (a tab each). Logged-in players also get buttons to get back
// into the game: start a new board, go back to the one they came from, or continue an
// unfinished one.
public class LeaderboardController(ILeaderboardService leaderboardService, IChallengeService challengeService) : Controller
{
    public const int PageSize = 20;

    // from = the board the player came from (the leaderboard button in the win dialog).
    [HttpGet]
    public async Task<IActionResult> Index(
        ChallengeKind kind = ChallengeKind.Easy, int page = 1, int? from = null, CancellationToken cancellationToken = default)
    {
        // A broken or old ?kind= just shows the first tab instead of an error.
        if (ModelState.GetFieldValidationState(nameof(kind)) == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid
            || !Enum.IsDefined(kind))
        {
            kind = ChallengeKind.Easy;
        }

        var entries = await leaderboardService.GetPageAsync(kind, Math.Max(1, page), PageSize, cancellationToken);

        int? unfinishedId = null;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            unfinishedId = (await challengeService.GetSummaryAsync(userId, cancellationToken)).UnfinishedId;
        }
        else
        {
            from = null; // visitors don't have any games to go back to
        }

        return View(new LeaderboardViewModel
        {
            Kind = kind,
            Entries = entries,
            // It's just a link. The challenge page checks the board really belongs to the player.
            FromChallengeId = from is > 0 ? from : null,
            UnfinishedChallengeId = unfinishedId == from ? null : unfinishedId,
        });
    }
}
