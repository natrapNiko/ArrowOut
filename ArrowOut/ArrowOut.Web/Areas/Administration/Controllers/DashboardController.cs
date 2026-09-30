using ArrowOut.Services.Levels;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Areas.Administration.Controllers;

public class DashboardController(ILevelAdminService levelAdminService) : AdministrationController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await levelAdminService.GetDashboardStatsAsync(cancellationToken));
}
