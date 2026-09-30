using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArrowOut.Data.Seeding;

public sealed class AdminUserSeeder : ISeeder
{
    public int Order => 20;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var options = serviceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var logger = serviceProvider.GetRequiredService<ILogger<AdminUserSeeder>>();

        if (string.IsNullOrWhiteSpace(options.AdminEmail))
        {
            logger.LogWarning("Seed:AdminEmail is not configured; skipping administrator seeding.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(options.AdminEmail);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(options.AdminPassword))
            {
                // No hard-coded fallback password. Outside Development it has to come from config.
                logger.LogWarning(
                    "No administrator exists and Seed:AdminPassword is empty. Set it via user secrets or environment variables.");
                return;
            }

            admin = new ApplicationUser
            {
                UserName = options.AdminEmail,
                Email = options.AdminEmail,
                EmailConfirmed = true,
                DisplayName = "Admin",
            };

            var created = await userManager.CreateAsync(admin, options.AdminPassword);
            SeedingException.ThrowIfFailed(created, $"create admin user '{options.AdminEmail}'");
        }

        foreach (var role in Roles.All)
        {
            if (!await userManager.IsInRoleAsync(admin, role))
            {
                var added = await userManager.AddToRoleAsync(admin, role);
                SeedingException.ThrowIfFailed(added, $"add admin to role '{role}'");
            }
        }
    }
}
