using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class TariffPlanConfiguration : IEntityTypeConfiguration<TariffPlan>
{
    public void Configure(EntityTypeBuilder<TariffPlan> builder)
    {
        builder.ToTable("TariffPlans");
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.Features).HasColumnType("jsonb").IsRequired();
    }
}

public class OrganizationTariffConfiguration : IEntityTypeConfiguration<OrganizationTariff>
{
    public void Configure(EntityTypeBuilder<OrganizationTariff> builder)
    {
        builder.ToTable("OrganizationTariffs");
        builder.HasIndex(t => new { t.OrganizationId, t.StartsAt });
        builder.HasOne(t => t.Organization).WithMany().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.TariffPlan).WithMany().HasForeignKey(t => t.TariffPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
