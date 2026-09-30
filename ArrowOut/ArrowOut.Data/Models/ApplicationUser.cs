using ArrowOut.Data.Models.Common;
using Microsoft.AspNetCore.Identity;

namespace ArrowOut.Data.Models;

public class ApplicationUser : IdentityUser, IAuditInfo
{
    public const string DefaultThemeKey = "classic";

    public string? DisplayName { get; set; }

    public string ThemeKey { get; set; } = DefaultThemeKey;

    public DateTime CreatedOn { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public virtual ICollection<PlayerProgress> Progress { get; private set; } = new HashSet<PlayerProgress>();

    // Falls back to the part of the e-mail before the @ when there's no display name.
    public string PublicName =>
        !string.IsNullOrWhiteSpace(DisplayName)
            ? DisplayName
            : (UserName?.Split('@')[0] ?? "Player");
}
