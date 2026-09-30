using System.ComponentModel.DataAnnotations;

namespace ArrowOut.Data.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    [EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    // Only needed the first time, to create the admin. Put it in user secrets or an env variable.
    public string AdminPassword { get; set; } = string.Empty;

    // Demo player. Skipped if either of these is empty.
    public string DemoEmail { get; set; } = string.Empty;

    public string DemoPassword { get; set; } = string.Empty;
}
