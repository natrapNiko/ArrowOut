using ArrowOut.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArrowOut.Data.Seeding;

public sealed class RolesSeeder : ISeeder
{
    public int Order => 10;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            SeedingException.ThrowIfFailed(result, $"create role '{roleName}'");
        }
    }
}
