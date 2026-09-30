using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArrowOut.Data.Configurations;

public class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.ToTable("Levels", table =>
        {
            // The UI and the services already check these, but the database gets the final say.
            table.HasCheckConstraint("CK_Levels_Number", $"[Number] BETWEEN {DataConstants.Level.NumberMin} AND {DataConstants.Level.NumberMax}");
            table.HasCheckConstraint("CK_Levels_Width", $"[Width] BETWEEN {DataConstants.Level.SizeMin} AND {DataConstants.Level.SizeMax}");
            table.HasCheckConstraint("CK_Levels_Height", $"[Height] BETWEEN {DataConstants.Level.SizeMin} AND {DataConstants.Level.SizeMax}");
            table.HasCheckConstraint("CK_Levels_MaxLives", $"[MaxLives] BETWEEN {DataConstants.Level.LivesMin} AND {DataConstants.Level.LivesMax}");
            table.HasCheckConstraint("CK_Levels_Difficulty", "[Difficulty] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_Levels_Name", "LEN([Name]) >= 3");
        });

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(DataConstants.Level.NameMaxLength);

        builder.HasIndex(l => l.Number).IsUnique();
        builder.HasIndex(l => l.Name);
        builder.HasIndex(l => new { l.IsPublished, l.Difficulty });

        builder.HasMany(l => l.Arrows)
            .WithOne(a => a.Level)
            .HasForeignKey(a => a.LevelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(l => l.Progress)
            .WithOne(p => p.Level)
            .HasForeignKey(p => p.LevelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
