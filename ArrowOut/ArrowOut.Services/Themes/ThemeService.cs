using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ArrowOut.Services.Themes;

public interface IThemeService
{
    Task EnsureDefaultThemesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ThemeDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    // Gets the theme, or the default one if it doesn't exist.
    Task<ThemeDefinition> GetOrDefaultAsync(string? key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    // Checks and saves an uploaded theme. Throws ThemeValidationException if it's no good.
    Task<ThemeDefinition> SaveAsync(Stream content, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    bool IsBuiltIn(string key);
}

public sealed class ThemeService(
    IFileStorage storage,
    IMemoryCache cache,
    ILogger<ThemeService> logger) : IThemeService
{
    public const string Container = "themes";
    public const string DefaultKey = "classic";
    public const long MaxFileBytes = 8 * 1024;

    private const string CacheKey = "themes:all";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        MaxDepth = 4,
    };

    private static readonly IReadOnlyList<ThemeDefinition> BuiltInThemes =
    [
        // Players have these keys saved on their accounts, so don't rename them.
        new() { Key = DefaultKey, Name = "Peach Morning", Colors = new ThemeColors() },
        new()
        {
            Key = "midnight",
            Name = "Cocoa Night",
            IsDark = true,
            Colors = new ThemeColors
            {
                // Cocoa: dark chocolate page, brown cards, cream text, orange accent.
                // In dark mode cartoon.css draws the outlines in near-black.
                Background = "#221a16", Surface = "#33271f", Grid = "#4d3b2f", Text = "#fff1dc",
                Muted = "#c9b39a", Arrow = "#fff1dc", Accent = "#ff9f43", Danger = "#ff6b5b", Success = "#7bd88f",
            },
        },
        new()
        {
            Key = "mint",
            Name = "Mint",
            Colors = new ThemeColors
            {
                Background = "#eef7f2", Surface = "#ffffff", Grid = "#bfdccc", Text = "#16352a",
                Muted = "#557565", Arrow = "#16352a", Accent = "#1d9a6c", Danger = "#d9534f", Success = "#1d9a6c",
            },
        },
        new()
        {
            Key = "sunset",
            Name = "Sunset",
            Colors = new ThemeColors
            {
                Background = "#fff4ec", Surface = "#ffffff", Grid = "#f0cdb4", Text = "#3b2418",
                Muted = "#8a6450", Arrow = "#3b2418", Accent = "#e2683c", Danger = "#c0392b", Success = "#3f9b5a",
            },
        },
    ];

    public bool IsBuiltIn(string key) => BuiltInThemes.Any(t => t.Key == key);

    public async Task EnsureDefaultThemesAsync(CancellationToken cancellationToken = default)
    {
        // Nobody can upload or edit the built-in themes, so the code is where they really live.
        // We rewrite them on every start so colour changes show up on existing installs too.
        foreach (var theme in BuiltInThemes)
        {
            await storage.WriteTextAsync(Container, FileNameFor(theme.Key), JsonSerializer.Serialize(theme, JsonOptions), cancellationToken);
        }

        cache.Remove(CacheKey);
    }

    public async Task<IReadOnlyList<ThemeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<ThemeDefinition>? cached) && cached is not null)
        {
            return cached;
        }

        var themes = new List<ThemeDefinition>();
        foreach (var fileName in await storage.ListAsync(Container, cancellationToken))
        {
            if (!fileName.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            var json = await storage.ReadTextAsync(Container, fileName, cancellationToken);
            var (theme, errors) = Parse(json ?? string.Empty);

            if (theme is null)
            {
                // A broken theme file should never break a page.
                logger.LogWarning("Skipping invalid theme file {File}: {Errors}", fileName, string.Join(" ", errors));
                continue;
            }

            themes.Add(theme);
        }

        if (themes.All(t => t.Key != DefaultKey))
        {
            themes.Insert(0, BuiltInThemes[0]);
        }

        IReadOnlyList<ThemeDefinition> result = themes.OrderBy(t => t.Key == DefaultKey ? 0 : 1).ThenBy(t => t.Name).ToList();
        cache.Set(CacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<ThemeDefinition> GetOrDefaultAsync(string? key, CancellationToken cancellationToken = default)
    {
        var themes = await GetAllAsync(cancellationToken);
        return themes.FirstOrDefault(t => t.Key == key)
            ?? themes.FirstOrDefault(t => t.Key == DefaultKey)
            ?? BuiltInThemes[0];
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).Any(t => t.Key == key);

    public async Task<ThemeDefinition> SaveAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var limited = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (limited.Length + read > MaxFileBytes)
            {
                throw new ThemeValidationException([$"Theme files may not exceed {MaxFileBytes / 1024} KB."]);
            }

            await limited.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var (theme, errors) = Parse(Encoding.UTF8.GetString(limited.ToArray()));
        if (theme is null)
        {
            throw new ThemeValidationException(errors);
        }

        if (IsBuiltIn(theme.Key))
        {
            throw new ThemeValidationException([$"'{theme.Key}' is a built-in theme and cannot be replaced."]);
        }

        // The file name comes from the checked key, never from the name of the uploaded file.
        await storage.WriteTextAsync(Container, FileNameFor(theme.Key), JsonSerializer.Serialize(theme, JsonOptions), cancellationToken);
        cache.Remove(CacheKey);
        return theme;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !System.Text.RegularExpressions.Regex.IsMatch(key, ThemeDefinition.KeyPattern))
        {
            throw new EntityNotFoundException("Theme", key ?? string.Empty);
        }

        if (IsBuiltIn(key))
        {
            throw new OperationNotAllowedException("Built-in themes cannot be deleted.");
        }

        if (!await storage.DeleteAsync(Container, FileNameFor(key), cancellationToken))
        {
            throw new EntityNotFoundException("Theme", key);
        }

        cache.Remove(CacheKey);
    }

    internal static (ThemeDefinition? Theme, IReadOnlyList<string> Errors) Parse(string json)
    {
        ThemeDefinition? theme;
        try
        {
            theme = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return (null, ["The file is not valid JSON."]);
        }

        if (theme is null)
        {
            return (null, ["The file is empty."]);
        }

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(theme, new ValidationContext(theme), results, validateAllProperties: true);

        if (theme.Colors is not null)
        {
            valid &= Validator.TryValidateObject(theme.Colors, new ValidationContext(theme.Colors), results, validateAllProperties: true);
        }

        return valid
            ? (theme, [])
            : (null, results.Select(r => r.ErrorMessage ?? "Invalid value.").ToList());
    }

    private static string FileNameFor(string key) => key + ".json";
}
