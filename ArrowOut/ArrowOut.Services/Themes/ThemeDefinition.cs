using System.ComponentModel.DataAnnotations;

namespace ArrowOut.Services.Themes;

// A colour theme, saved as a JSON file. Every colour has to be a plain #RRGGBB value. That's
// what makes it safe to put the theme in a <style> block without anyone sneaking in CSS or HTML.
public sealed class ThemeDefinition
{
    public const string KeyPattern = "^[a-z0-9][a-z0-9-]{2,39}$";

    [Required]
    [RegularExpression(KeyPattern, ErrorMessage = "Key: 3–40 lowercase letters, digits or dashes.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(40, MinimumLength = 2)]
    [RegularExpression(@"^[\p{L}\p{N} '\-]+$", ErrorMessage = "Name: letters, digits, spaces and dashes only.")]
    public string Name { get; set; } = string.Empty;

    public bool IsDark { get; set; }

    [Required]
    public ThemeColors Colors { get; set; } = new();
}

// The colours. The defaults here are "Peach Morning", the light cartoon theme.
public sealed class ThemeColors
{
    public const string HexPattern = "^#[0-9a-fA-F]{6}$";
    private const string HexMessage = "{0} must be a #RRGGBB colour.";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Background { get; set; } = "#ffe8d9";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Surface { get; set; } = "#ffffff";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Grid { get; set; } = "#f3c7ad";

    // Also used for the cartoon outlines and hard shadows.
    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Text { get; set; } = "#3b1f14";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Muted { get; set; } = "#8a5a44";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Arrow { get; set; } = "#3b1f14";

    // Coral. Hard boards use the darker Danger red so the two don't look the same.
    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Accent { get; set; } = "#ff5e57";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Danger { get; set; } = "#c0392b";

    [Required, RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string Success { get; set; } = "#2e9d5b";
}
