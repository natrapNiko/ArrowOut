using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArrowOut.Data.Seeding;

// Runs all the ISeeder steps, lowest Order first.
public sealed class ApplicationDbContextSeeder(IEnumerable<ISeeder> seeders, ILogger<ApplicationDbContextSeeder> logger)
{
    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            logger.LogInformation("Running seeder {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(dbContext, serviceProvider, cancellationToken);
        }
    }
}

public static class SeedingServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseSeeding(this IServiceCollection services)
    {
        services.AddScoped<ISeeder, RolesSeeder>();
        services.AddScoped<ISeeder, AdminUserSeeder>();
        services.AddScoped<ISeeder, DemoUserSeeder>();
        services.AddScoped<ApplicationDbContextSeeder>();
        return services;
    }
}
