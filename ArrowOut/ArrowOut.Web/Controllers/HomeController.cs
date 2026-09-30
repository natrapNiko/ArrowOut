using ArrowOut.Services.Challenges;
using ArrowOut.Web.Infrastructure;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

public class HomeController(IChallengeService challengeService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new HomeViewModel
        {
            Summary = User.Identity?.IsAuthenticated == true
                ? await challengeService.GetSummaryAsync(User.GetRequiredUserId(), cancellationToken)
                : null,
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult HowToPlay() => View();

    [HttpGet]
    public IActionResult Privacy() => View();
}
