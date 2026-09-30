using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArrowOut.Data.Seeding;

// Normal player account (no admin) for showing the game to people. Only created once.
public sealed class DemoUserSeeder : ISeeder
{
    public int Order => 30;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var options = serviceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.DemoEmail) || string.IsNullOrWhiteSpace(options.DemoPassword))
        {
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(options.DemoEmail) is not null)
        {
            return;
        }

        var demo = new ApplicationUser
        {
            UserName = options.DemoEmail,
            Email = options.DemoEmail,
            EmailConfirmed = true,
            DisplayName = "Demo Player",
        };

        var created = await userManager.CreateAsync(demo, options.DemoPassword);
        SeedingException.ThrowIfFailed(created, $"create demo user '{options.DemoEmail}'");

        var added = await userManager.AddToRoleAsync(demo, Roles.Player);
        SeedingException.ThrowIfFailed(added, $"add demo user to role '{Roles.Player}'");
    }
}
