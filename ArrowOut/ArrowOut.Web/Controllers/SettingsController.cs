using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Themes;
using ArrowOut.Services.Users;
using ArrowOut.Web.Infrastructure;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

[Authorize]
public class SettingsController(IPlayerSettingsService settingsService, IThemeService themeService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var input = await settingsService.GetAsync(User.GetRequiredUserId(), cancellationToken);
        return View(await BuildModelAsync(input, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([Bind(Prefix = "Input")] PlayerSettingsModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(input, cancellationToken));
        }

        try
        {
            await settingsService.UpdateAsync(User.GetRequiredUserId(), input, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            ModelState.AddModelError("Input.ThemeKey", "Please choose one of the available themes.");
            return View(await BuildModelAsync(input, cancellationToken));
        }

        TempData[StatusMessages.SuccessKey] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<SettingsViewModel> BuildModelAsync(PlayerSettingsModel input, CancellationToken cancellationToken) =>
        new() { Input = input, Themes = await themeService.GetAllAsync(cancellationToken) };
}
