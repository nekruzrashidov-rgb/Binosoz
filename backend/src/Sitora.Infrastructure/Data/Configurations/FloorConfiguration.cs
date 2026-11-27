using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("Floors");
        builder.Property(f => f.PlanImageUrl).HasMaxLength(500);
        builder.HasIndex(f => new { f.BuildingId, f.Number }).IsUnique();
        builder.HasOne(f => f.Building)
            .WithMany(b => b.Floors)
            .HasForeignKey(f => f.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LayoutConfiguration : IEntityTypeConfiguration<Layout>
{
    public void Configure(EntityTypeBuilder<Layout> builder)
    {
        builder.ToTable("Layouts");
        builder.Property(l => l.Code).HasMaxLength(Layout.CodeMaxLength).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Json).HasColumnType("jsonb").IsRequired();
        // Один код — одна планировка организации.
        builder.HasIndex(l => new { l.OrganizationId, l.Code }).IsUnique();
        builder.HasOne(l => l.Organization).WithMany().HasForeignKey(l => l.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FloorPlanConfiguration : IEntityTypeConfiguration<FloorPlan>
{
    public void Configure(EntityTypeBuilder<FloorPlan> builder)
    {
        builder.ToTable("FloorPlans");
        // Один план на этаж дома.
        builder.HasIndex(x => new { x.BuildingId, x.Floor }).IsUnique();
        builder.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
    }
}
