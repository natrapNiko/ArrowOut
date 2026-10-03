using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using ArrowOut.Game.Generation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArrowOut.Data.Configurations;

public class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        builder.ToTable("Challenges", table =>
        {
            table.HasCheckConstraint("CK_Challenges_Width", $"[Width] BETWEEN {DataConstants.Level.SizeMin} AND {DataConstants.Level.SizeMax}");
            table.HasCheckConstraint("CK_Challenges_Height", $"[Height] BETWEEN {DataConstants.Level.SizeMin} AND {DataConstants.Level.SizeMax}");
            table.HasCheckConstraint("CK_Challenges_MaxLives", $"[MaxLives] BETWEEN {DataConstants.Level.LivesMin} AND {DataConstants.Level.LivesMax}");
            table.HasCheckConstraint("CK_Challenges_ArrowCount", "[ArrowCount] >= 1");
            table.HasCheckConstraint("CK_Challenges_Stars", $"[Stars] BETWEEN 0 AND {DataConstants.Progress.StarsMax}");
            table.HasCheckConstraint("CK_Challenges_BestMistakes", "[BestMistakes] IS NULL OR [BestMistakes] >= 0");
            table.HasCheckConstraint("CK_Challenges_Attempts", "[Attempts] >= 0");
            table.HasCheckConstraint("CK_Challenges_Kind", "[Kind] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_Challenges_Points", "[Points] >= 0 AND [HintsThisAttempt] >= 0");
        });

        // Old rows from before we had kinds were all big boards, so they default to Hard.
        builder.Property(c => c.Kind).HasDefaultValue(ChallengeKind.Hard).HasSentinel((ChallengeKind)(-1));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.OwnerId).IsRequired();

        // nvarchar(max) so even a 700-arrow board fits in one row.
        builder.Property(c => c.Layout).IsRequired();

        builder.HasIndex(c => new { c.OwnerId, c.CreatedOn });
        // For the leaderboard: won boards of one kind, grouped by player.
        builder.HasIndex(c => new { c.Kind, c.IsCompleted, c.OwnerId }).IncludeProperties(c => new { c.Points, c.Stars });

        builder.HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
