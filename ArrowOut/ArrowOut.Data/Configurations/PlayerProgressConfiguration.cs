using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArrowOut.Data.Configurations;

public class PlayerProgressConfiguration : IEntityTypeConfiguration<PlayerProgress>
{
    public void Configure(EntityTypeBuilder<PlayerProgress> builder)
    {
        builder.ToTable("PlayerProgress", table =>
        {
            table.HasCheckConstraint("CK_PlayerProgress_Stars", $"[Stars] BETWEEN 0 AND {DataConstants.Progress.StarsMax}");
            table.HasCheckConstraint("CK_PlayerProgress_BestMistakes", "[BestMistakes] IS NULL OR [BestMistakes] >= 0");
            table.HasCheckConstraint("CK_PlayerProgress_Counters", "[Attempts] >= 0 AND [Completions] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();

        builder.HasIndex(p => new { p.UserId, p.LevelId }).IsUnique();

        builder.HasOne(p => p.User)
            .WithMany(u => u.Progress)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
