using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.Property(c => c.FullName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.PassportDataEncrypted).HasColumnName("PassportData").HasMaxLength(2000);
        builder.HasIndex(c => c.Phone).IsUnique();
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        builder.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
