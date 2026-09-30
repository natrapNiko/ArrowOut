using System.Security.Claims;
using ArrowOut.Services.Themes;
using ArrowOut.Services.Users;
using ArrowOut.Web.Controllers;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArrowOut.Tests.Web;

public class ThemeModeTests
{
    private static readonly ThemeDefinition Classic = new() { Key = "classic", Name = "Classic Paper" };
    private static readonly ThemeDefinition Mint = new() { Key = "mint", Name = "Mint" };
    private static readonly ThemeDefinition Midnight = new() { Key = "midnight", Name = "Midnight", IsDark = true };

    private readonly Mock<IThemeService> _themes = new();
    private readonly Mock<IPlayerSettingsService> _settings = new();

    public ThemeModeTests()
    {
        var all = new[] { Classic, Mint, Midnight }.ToDictionary(t => t.Key);
        _themes.Setup(t => t.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => all.ContainsKey(key));
        _themes.Setup(t => t.GetOrDefaultAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? key, CancellationToken _) => key is not null && all.TryGetValue(key, out var theme) ? theme : Classic);
    }

    private ThemeResolver Resolver() => new(_themes.Object, _settings.Object, NullLogger<ThemeResolver>.Instance);

    private static DefaultHttpContext Visitor(string? themeCookie = null)
    {
        var context = new DefaultHttpContext();
        if (themeCookie is not null)
        {
            context.Request.Headers.Cookie = $"{ThemeResolver.CookieName}={themeCookie}";
        }

        return context;
    }

    private static DefaultHttpContext Player(string userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")),
    };

    // ---------- Resolver ----------

    [Fact]
    public async Task NewVisitor_HasNoChosenMode_SoTheDeviceSettingDecides()
    {
        var theme = await Resolver().ResolveAsync(Visitor());

        Assert.Null(theme.Mode);
        Assert.Equal(("classic", "midnight"), (theme.Light.Key, theme.Dark.Key));
    }

    [Fact]
    public async Task Visitor_WithDarkCookie_GetsDarkMode()
    {
        var theme = await Resolver().ResolveAsync(Visitor("midnight"));

        Assert.Equal(ResolvedTheme.DarkMode, theme.Mode);
        Assert.Same(Midnight, theme.Initial);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("<script>")]
    [InlineData("")]
    public async Task Visitor_WithBogusCookie_IsTreatedAsNoChoice(string cookie)
    {
        var theme = await Resolver().ResolveAsync(Visitor(cookie));

        Assert.Null(theme.Mode);
    }

    [Fact]
    public async Task Player_LightThemeIsKept_AndPairedWithTheDefaultDarkTheme()
    {
        _settings.Setup(s => s.GetThemeKeyAsync("u", It.IsAny<CancellationToken>())).ReturnsAsync("mint");

        var theme = await Resolver().ResolveAsync(Player("u"));

        Assert.Equal(ResolvedTheme.LightMode, theme.Mode);
        Assert.Equal(("mint", "midnight"), (theme.Light.Key, theme.Dark.Key));
    }

    [Fact]
    public async Task Resolver_NeverFailsAPage_WhenThemesCannotBeLoaded()
    {
        _themes.Setup(t => t.GetOrDefaultAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("disk"));

        var theme = await Resolver().ResolveAsync(Visitor("midnight"));

        Assert.False(theme.Light.IsDark);
        Assert.True(theme.Dark.IsDark);
    }

    // ---------- Switch endpoint ----------

    private ThemeModeController Controller(HttpContext context)
    {
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string?>())).Returns((string? u) => u is not null && u.StartsWith('/') && !u.StartsWith("//", StringComparison.Ordinal));
        url.Setup(u => u.Content("~/")).Returns("/");

        return new ThemeModeController(Resolver(), _settings.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            Url = url.Object,
        };
    }

    [Fact]
    public async Task Visitor_SwitchingToDark_IsRememberedInASecureCookie()
    {
        var context = Visitor();
        context.Request.Headers[ThemeModeController.FetchHeader] = "fetch";

        var result = await Controller(context).Set("dark", null, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{ThemeResolver.CookieName}=midnight", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Player_SwitchingToLight_SavesTheLightThemeOnTheAccount()
    {
        _settings.Setup(s => s.GetThemeKeyAsync("u", It.IsAny<CancellationToken>())).ReturnsAsync("midnight");

        var result = await Controller(Player("u")).Set("light", "/leaderboard", CancellationToken.None);

        _settings.Verify(s => s.SetThemeAsync("u", "classic", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("/leaderboard", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public async Task Switch_RejectsUnknownModes_AndForeignReturnUrls()
    {
        Assert.IsType<BadRequestResult>(await Controller(Visitor()).Set("purple", null, CancellationToken.None));

        var result = await Controller(Visitor()).Set("dark", "https://evil.example/", CancellationToken.None);
        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(result).Url);
    }
}
