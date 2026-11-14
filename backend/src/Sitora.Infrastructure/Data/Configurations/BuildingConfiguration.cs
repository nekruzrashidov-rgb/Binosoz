using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");
        builder.Property(b => b.NameRu).HasMaxLength(200).IsRequired();
        builder.Property(b => b.NameTg).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Address).HasMaxLength(300).IsRequired();
        builder.Property(b => b.DescriptionRu).HasMaxLength(4000);
        builder.Property(b => b.DescriptionTg).HasMaxLength(4000);
        builder.Property(b => b.ModelUrl).HasMaxLength(500);
        // Эко-паспорт — в той же таблице, столбцы Eco*; все необязательные (не заполнен — Eco = null).
        builder.OwnsOne(b => b.Eco, eco =>
        {
            eco.Property(e => e.EnergyClass).HasColumnName("EcoEnergyClass").HasConversion<string>().HasMaxLength(16);
            eco.Property(e => e.WallInsulationRu).HasColumnName("EcoWallInsulationRu").HasMaxLength(200);
            eco.Property(e => e.WallInsulationTg).HasColumnName("EcoWallInsulationTg").HasMaxLength(200);
            eco.Property(e => e.WindowsRu).HasColumnName("EcoWindowsRu").HasMaxLength(200);
            eco.Property(e => e.WindowsTg).HasColumnName("EcoWindowsTg").HasMaxLength(200);
            eco.Property(e => e.SolarPanelsKw).HasColumnName("EcoSolarPanelsKw").HasPrecision(18, 2);
            eco.Property(e => e.HasHeatMeters).HasColumnName("EcoHasHeatMeters");
            eco.Property(e => e.HasWaterMeters).HasColumnName("EcoHasWaterMeters");
            eco.Property(e => e.HasLedCommonAreas).HasColumnName("EcoHasLedCommonAreas");
            eco.Property(e => e.HasEvCharging).HasColumnName("EcoHasEvCharging");
            eco.Property(e => e.HasRainwaterReuse).HasColumnName("EcoHasRainwaterReuse");
            eco.Property(e => e.HasWasteSorting).HasColumnName("EcoHasWasteSorting");
            eco.Property(e => e.GreenAreaM2).HasColumnName("EcoGreenAreaM2").HasPrecision(18, 2);
            eco.Property(e => e.HeatingType).HasColumnName("EcoHeatingType").HasConversion<string>().HasMaxLength(32);
            eco.Property(e => e.EstimatedKwhPerM2Year).HasColumnName("EcoEstimatedKwhPerM2Year").HasPrecision(18, 2);
        });
        builder.HasOne(b => b.Organization).WithMany(o => o.Buildings)
            .HasForeignKey(b => b.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
