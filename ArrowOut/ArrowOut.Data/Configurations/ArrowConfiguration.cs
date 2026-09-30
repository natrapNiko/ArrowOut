using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArrowOut.Data.Configurations;

public class ArrowConfiguration : IEntityTypeConfiguration<Arrow>
{
    public void Configure(EntityTypeBuilder<Arrow> builder)
    {
        builder.ToTable("Arrows", table =>
        {
            table.HasCheckConstraint("CK_Arrows_X", $"[X] BETWEEN 0 AND {DataConstants.Level.SizeMax - 1}");
            table.HasCheckConstraint("CK_Arrows_Y", $"[Y] BETWEEN 0 AND {DataConstants.Level.SizeMax - 1}");
            table.HasCheckConstraint("CK_Arrows_Direction", "[Direction] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_Arrows_Length", $"[Length] BETWEEN {DataConstants.Arrow.LengthMin} AND {DataConstants.Arrow.LengthMax}");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Path)
            .IsRequired()
            .HasMaxLength(Arrow.PathMaxLength);

        // Two arrow heads can't sit on the same cell in one level.
        builder.HasIndex(a => new { a.LevelId, a.X, a.Y }).IsUnique();
    }
}
