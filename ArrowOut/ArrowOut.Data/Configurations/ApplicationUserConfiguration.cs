using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArrowOut.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Ignore(u => u.PublicName);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(DataConstants.User.DisplayNameMaxLength);

        builder.Property(u => u.ThemeKey)
            .IsRequired()
            .HasMaxLength(DataConstants.User.ThemeKeyMaxLength)
            .HasDefaultValue(ApplicationUser.DefaultThemeKey);
    }
}
