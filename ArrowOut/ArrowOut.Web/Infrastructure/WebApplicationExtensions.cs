using ArrowOut.Data;
using ArrowOut.Data.Seeding;
using ArrowOut.Services.Themes;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Web.Infrastructure;

public static class WebApplicationExtensions
{
    // Runs any new migrations, then the seeders (roles, admin, demo player) and the built-in themes.
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();

            var seeder = services.GetRequiredService<ApplicationDbContextSeeder>();
            await seeder.SeedAsync(dbContext, services);

            await services.GetRequiredService<IThemeService>().EnsureDefaultThemesAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database initialisation failed. Check the connection string and SQL Server availability.");
            throw;
        }
    }
}
