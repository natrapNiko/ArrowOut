using System.ComponentModel.DataAnnotations;
using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Themes;
using Microsoft.AspNetCore.Identity;

namespace ArrowOut.Services.Users;

public sealed class PlayerSettingsModel
{
    [Display(Name = "Display name")]
    [StringLength(DataConstants.User.DisplayNameMaxLength, MinimumLength = DataConstants.User.DisplayNameMinLength)]
    [RegularExpression(DataConstants.User.DisplayNamePattern, ErrorMessage = "Use letters, digits, spaces, dots, dashes or underscores.")]
    public string? DisplayName { get; set; }

    [Required]
    [Display(Name = "Theme")]
    [RegularExpression(ThemeDefinition.KeyPattern)]
    public string ThemeKey { get; set; } = ThemeService.DefaultKey;
}

public interface IPlayerSettingsService
{
    Task<PlayerSettingsModel> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(string userId, PlayerSettingsModel model, CancellationToken cancellationToken = default);

    Task<string> GetThemeKeyAsync(string? userId, CancellationToken cancellationToken = default);

    // Only changes the theme (for the dark/light button). Throws if the theme doesn't exist.
    Task SetThemeAsync(string userId, string themeKey, CancellationToken cancellationToken = default);
}

public sealed class PlayerSettingsService(
    UserManager<ApplicationUser> userManager,
    IThemeService themeService) : IPlayerSettingsService
{
    public async Task<PlayerSettingsModel> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);
        return new PlayerSettingsModel { DisplayName = user.DisplayName, ThemeKey = user.ThemeKey };
    }

    public async Task UpdateAsync(string userId, PlayerSettingsModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Make sure the theme really exists on the server, whatever the form sent.
        if (!await themeService.ExistsAsync(model.ThemeKey, cancellationToken))
        {
            throw new EntityNotFoundException("Theme", model.ThemeKey);
        }

        var user = await FindAsync(userId);
        user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? null : model.DisplayName.Trim();
        user.ThemeKey = model.ThemeKey;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new OperationNotAllowedException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<string> GetThemeKeyAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return ThemeService.DefaultKey;
        }

        var user = await userManager.FindByIdAsync(userId);
        return user?.ThemeKey ?? ThemeService.DefaultKey;
    }

    public async Task SetThemeAsync(string userId, string themeKey, CancellationToken cancellationToken = default)
    {
        if (!await themeService.ExistsAsync(themeKey, cancellationToken))
        {
            throw new EntityNotFoundException("Theme", themeKey);
        }

        var user = await FindAsync(userId);
        if (user.ThemeKey == themeKey)
        {
            return;
        }

        user.ThemeKey = themeKey;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new OperationNotAllowedException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task<ApplicationUser> FindAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return await userManager.FindByIdAsync(userId) ?? throw new EntityNotFoundException("User", userId);
    }
}
