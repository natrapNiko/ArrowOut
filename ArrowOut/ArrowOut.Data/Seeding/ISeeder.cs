namespace ArrowOut.Data.Seeding;

// One seeding step. Runs on every start-up, so it has to check what's already there.
public interface ISeeder
{
    // Lower runs first.
    int Order { get; }

    Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
