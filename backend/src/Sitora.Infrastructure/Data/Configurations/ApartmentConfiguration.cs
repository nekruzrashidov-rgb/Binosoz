using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public const string PlanSlotIndex = "IX_Apartments_FloorId_PlanSlot";
    // Номер уникален в пределах дома (блока), у каждой нумерации своя серия: квартиры и пентхаусы, магазины (Н-), машино-места (М-).
    public const string HomeNumberIndex = "IX_Apartments_BuildingId_Number_Home";
    public const string ShopNumberIndex = "IX_Apartments_BuildingId_Number_Commercial";
    public const string SpotNumberIndex = "IX_Apartments_BuildingId_Number_Parking";

    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("Apartments", t => t.HasCheckConstraint(
            "CK_Apartments_PlanSlot", $"\"PlanSlot\" BETWEEN 0 AND {Apartment.MaxPlanSlot}"));
        builder.Property(a => a.Model3dId).HasMaxLength(Apartment.Model3dIdMaxLength);
        // Вид помещения строкой, по умолчанию — квартира (у всех прежних строк).
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(16).HasDefaultValue(Sitora.Domain.Enums.ApartmentKind.Residential);
        builder.HasIndex(a => new { a.FloorId, a.Number }).IsUnique();
        // BuildingId — дом этажа; ставит AppDbContext при сохранении (ApartmentBuildings), seed.sql — сам.
        builder.HasIndex(a => new { a.BuildingId, a.Number }, HomeNumberIndex).IsUnique()
            .HasFilter("\"Kind\" IN ('Residential', 'Penthouse')");
        builder.HasIndex(a => new { a.BuildingId, a.Number }, ShopNumberIndex).IsUnique().HasFilter("\"Kind\" = 'Commercial'");
        builder.HasIndex(a => new { a.BuildingId, a.Number }, SpotNumberIndex).IsUnique().HasFilter("\"Kind\" = 'Parking'");
        // Одна позиция на плане этажа — одна квартира.
        builder.HasIndex(a => new { a.FloorId, a.PlanSlot }).HasDatabaseName(PlanSlotIndex).IsUnique();
        builder.HasIndex(a => a.Status);
        builder.HasOne(a => a.Floor)
            .WithMany(f => f.Apartments)
            .HasForeignKey(a => a.FloorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
