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
        // Телефон уникален в пределах застройщика: у двух застройщиков один человек — две записи.
        builder.HasIndex(c => new { c.OrganizationId, c.Phone }).IsUnique();
        // Одна учётная запись покупателя может быть привязана к клиентам разных застройщиков.
        builder.HasIndex(c => c.UserId).HasFilter("\"UserId\" IS NOT NULL");
        builder.HasIndex(c => c.Email).HasFilter("\"Email\" IS NOT NULL");
        builder.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Organization>().WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Building>().WithMany().HasForeignKey(c => c.HomeBuildingId).OnDelete(DeleteBehavior.SetNull);
    }
}
