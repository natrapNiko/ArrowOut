using System.Security.Claims;
using ArrowOut.Services.Challenges;
using ArrowOut.Services.Leaderboard;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

// One public ranking for all games. Logged-in players also get buttons to get back
// into the game: start a new board, go back to the one they came from, or continue an
// unfinished one.
public class LeaderboardController(ILeaderboardService leaderboardService, IChallengeService challengeService) : Controller
{
    public const int PageSize = 20;

    // from = the board the player came from (the leaderboard button in the win dialog).
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int? from = null, CancellationToken cancellationToken = default)
    {
        var entries = await leaderboardService.GetPageAsync(Math.Max(1, page), PageSize, cancellationToken);

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
            Entries = entries,
            // It's just a link. The game page checks the board really belongs to the player.
            FromChallengeId = from is > 0 ? from : null,
            UnfinishedChallengeId = unfinishedId == from ? null : unfinishedId,
        });
    }
}
