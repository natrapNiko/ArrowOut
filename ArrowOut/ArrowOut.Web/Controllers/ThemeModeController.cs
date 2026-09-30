using System.Security.Claims;
using ArrowOut.Services.Users;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

// The dark/light button in the menu. The page has already switched in the browser, this just
// remembers it: on the account if you're logged in, in a cookie if not. Also works without
// JavaScript (normal form post, then back to the same page).
[AllowAnonymous]
[Route("theme")]
public class ThemeModeController(IThemeResolver themeResolver, IPlayerSettingsService settingsService) : Controller
{
    public const string FetchHeader = "X-Requested-With";

    [HttpPost("mode")]
    public async Task<IActionResult> Set([FromForm] string? mode, [FromForm] string? returnUrl, CancellationToken cancellationToken)
    {
        if (mode is not (ResolvedTheme.LightMode or ResolvedTheme.DarkMode))
        {
            return BadRequest();
        }

        var current = await themeResolver.ResolveAsync(HttpContext);
        var theme = mode == ResolvedTheme.DarkMode ? current.Dark : current.Light;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            await settingsService.SetThemeAsync(userId, theme.Key, cancellationToken);
        }
        else
        {
            Response.Cookies.Append(ThemeResolver.CookieName, theme.Key, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true, // the visitor chose this themselves, so no consent needed
                MaxAge = TimeSpan.FromDays(365),
            });
        }

        if (Request.Headers[FetchHeader] == "fetch")
        {
            return NoContent();
        }

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/"));
    }
}
