using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Themes;
using ArrowOut.Web.Areas.Administration.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Areas.Administration.Controllers;

// Custom colour themes. They're saved as JSON files, not in the database.
public class ThemesController(IThemeService themeService) : AdministrationController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new ThemesIndexViewModel
        {
            Themes = await themeService.GetAllAsync(cancellationToken),
            IsBuiltIn = themeService.IsBuiltIn,
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ThemeService.MaxFileBytes + 8_192)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            Error("Choose a theme JSON file to upload.");
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > ThemeService.MaxFileBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            Error($"Only .json files up to {ThemeService.MaxFileBytes / 1024} KB are accepted.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var theme = await themeService.SaveAsync(stream, cancellationToken);
            Success($"Theme \"{theme.Name}\" saved.");
        }
        catch (ThemeValidationException ex)
        {
            Error(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        try
        {
            await themeService.DeleteAsync(key, cancellationToken);
            Success("Theme deleted. Players using it fall back to the default theme.");
        }
        catch (OperationNotAllowedException ex)
        {
            Error(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }
}
