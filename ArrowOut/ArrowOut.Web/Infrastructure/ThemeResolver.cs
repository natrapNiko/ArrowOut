using System.Security.Claims;
using System.Text.RegularExpressions;
using ArrowOut.Services.Themes;
using ArrowOut.Services.Users;

namespace ArrowOut.Web.Infrastructure;

// The colours for one request: a light and a dark set (so the browser can switch instantly)
// and the Mode the player picked. Mode is null if they haven't picked one yet, and then
// the page just follows the device's light/dark setting.
public sealed record ResolvedTheme(ThemeDefinition Light, ThemeDefinition Dark, string? Mode)
{
    public const string LightMode = "light";
    public const string DarkMode = "dark";

    // The colours used before any JavaScript runs.
    public ThemeDefinition Initial => Mode == DarkMode ? Dark : Light;
}

public interface IThemeResolver
{
    Task<ResolvedTheme> ResolveAsync(HttpContext httpContext);
}

// Logged-in players get their saved theme, visitors get the one from the theme cookie.
// A light theme gets paired with the default dark one and the other way round, so the
// button always has something to switch to. Worked out once per request and then reused.
public sealed partial class ThemeResolver(
    IThemeService themeService,
    IPlayerSettingsService settingsService,
    ILogger<ThemeResolver> logger) : IThemeResolver
{
    public const string CookieName = "ao-theme";
    public const string DefaultDarkKey = "midnight";

    private const string ItemKey = "ao-resolved-theme";

    private static readonly ThemeDefinition FallbackLight = new()
    {
        Key = ThemeService.DefaultKey,
        Name = "Peach Morning",
        Colors = new ThemeColors(),
    };

    private static readonly ThemeDefinition FallbackDark = new()
    {
        Key = DefaultDarkKey,
        Name = "Cocoa Night",
        IsDark = true,
        Colors = new ThemeColors
        {
            Background = "#221a16", Surface = "#33271f", Grid = "#4d3b2f", Text = "#fff1dc",
            Muted = "#c9b39a", Arrow = "#fff1dc", Accent = "#ff9f43", Danger = "#ff6b5b", Success = "#7bd88f",
        },
    };

    public async Task<ResolvedTheme> ResolveAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Items[ItemKey] is ResolvedTheme cached)
        {
            return cached;
        }

        ResolvedTheme resolved;
        try
        {
            resolved = await ResolveCoreAsync(httpContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The layout also draws the error pages, so a theme problem must never break a page.
            logger.LogWarning(ex, "Falling back to the default themes");
            resolved = new ResolvedTheme(FallbackLight, FallbackDark, null);
        }

        httpContext.Items[ItemKey] = resolved;
        return resolved;
    }

    private async Task<ResolvedTheme> ResolveCoreAsync(HttpContext httpContext)
    {
        var cancellationToken = httpContext.RequestAborted;
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? key = null;
        if (!string.IsNullOrEmpty(userId))
        {
            key = await settingsService.GetThemeKeyAsync(userId, cancellationToken);
        }
        else if (httpContext.Request.Cookies.TryGetValue(CookieName, out var cookie)
            && KeyRegex().IsMatch(cookie)
            && await themeService.ExistsAsync(cookie, cancellationToken))
        {
            key = cookie;
        }

        var chosen = key is null ? null : await themeService.GetOrDefaultAsync(key, cancellationToken);

        var light = chosen is { IsDark: false } ? chosen : await themeService.GetOrDefaultAsync(ThemeService.DefaultKey, cancellationToken);
        var dark = chosen is { IsDark: true } ? chosen : await themeService.GetOrDefaultAsync(DefaultDarkKey, cancellationToken);

        return new ResolvedTheme(
            light.IsDark ? FallbackLight : light,
            dark.IsDark ? dark : FallbackDark,
            chosen is null ? null : chosen.IsDark ? ResolvedTheme.DarkMode : ResolvedTheme.LightMode);
    }

    [GeneratedRegex(ThemeDefinition.KeyPattern)]
    private static partial Regex KeyRegex();
}
