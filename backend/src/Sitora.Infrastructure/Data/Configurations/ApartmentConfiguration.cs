using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public const string PlanSlotIndex = "IX_Apartments_FloorId_PlanSlot";

    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("Apartments", t => t.HasCheckConstraint(
            "CK_Apartments_PlanSlot", $"\"PlanSlot\" BETWEEN 0 AND {Apartment.MaxPlanSlot}"));
        builder.Property(a => a.Model3dId).HasMaxLength(Apartment.Model3dIdMaxLength);
        builder.HasIndex(a => new { a.FloorId, a.Number }).IsUnique();
        // Одна позиция на плане этажа — одна квартира.
        builder.HasIndex(a => new { a.FloorId, a.PlanSlot }).HasDatabaseName(PlanSlotIndex).IsUnique();
        builder.HasIndex(a => a.Status);
        builder.HasOne(a => a.Floor)
            .WithMany(f => f.Apartments)
            .HasForeignKey(a => a.FloorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
