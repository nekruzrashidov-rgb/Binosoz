using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");

        // У покупателей почты нет, поэтому уникальность почты проверяет база, а не Identity.
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex")
            .IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");

        builder.HasOne(u => u.Organization).WithMany()
            .HasForeignKey(u => u.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
