using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Phone).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Comment).HasMaxLength(1000);
        builder.HasIndex(l => new { l.Status, l.CreatedAt });
        builder.HasOne(l => l.Apartment).WithMany().HasForeignKey(l => l.ApartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(l => l.Manager).WithMany().HasForeignKey(l => l.ManagerId).OnDelete(DeleteBehavior.SetNull);
    }
}
