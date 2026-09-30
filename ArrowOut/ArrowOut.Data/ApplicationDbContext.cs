using ArrowOut.Data.Models;
using ArrowOut.Data.Models.Common;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly TimeProvider _timeProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : this(options, TimeProvider.System)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, TimeProvider timeProvider)
        : base(options)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DbSet<Level> Levels => Set<Level>();

    public DbSet<Arrow> Arrows => Set<Arrow>();

    public DbSet<PlayerProgress> PlayerProgress => Set<PlayerProgress>();

    public DbSet<Challenge> Challenges => Set<Challenge>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditInfo();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    private void ApplyAuditInfo()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<IAuditInfo>())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.CreatedOn == default:
                    entry.Entity.CreatedOn = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedOn = now;
                    break;
            }
        }
    }
}
