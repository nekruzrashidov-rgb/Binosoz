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
        builder.Property(b => b.NameEn).HasMaxLength(200);
        builder.Property(b => b.DescriptionEn).HasMaxLength(4000);
        builder.Property(b => b.ModelUrl).HasMaxLength(500);
        // Удобства — массив ключей (text[]), по умолчанию пустой.
        // Координаты — 6 знаков после запятой (≈ 0,1 м), высота этажа — до сантиметра.
        builder.Property(b => b.Latitude).HasPrecision(9, 6);
        builder.Property(b => b.Longitude).HasPrecision(9, 6);
        builder.Property(b => b.FloorHeight).HasPrecision(4, 2);
        builder.Property(b => b.Amenities).HasColumnType("text[]").HasDefaultValueSql("'{}'::text[]").IsRequired();
        // Блок ЖК: контур, место на участке и уступы — jsonb; буква блока до 4 символов.
        builder.Property(b => b.Footprint).HasJson();
        builder.Property(b => b.SitePosition).HasJson();
        builder.Property(b => b.Setbacks).HasJson();
        builder.Property(b => b.BlockCode).HasMaxLength(4);
        builder.Property(b => b.SortOrder).HasDefaultValue(0);

        // Эко-паспорт — в той же таблице, столбцы Eco*; все необязательные (не заполнен — Eco = null).
        builder.OwnsOne(b => b.Eco, eco =>
        {
            eco.Property(e => e.EnergyClass).HasColumnName("EcoEnergyClass").HasConversion<string>().HasMaxLength(16);
            eco.Property(e => e.WallInsulationRu).HasColumnName("EcoWallInsulationRu").HasMaxLength(200);
            eco.Property(e => e.WallInsulationTg).HasColumnName("EcoWallInsulationTg").HasMaxLength(200);
            eco.Property(e => e.WindowsRu).HasColumnName("EcoWindowsRu").HasMaxLength(200);
            eco.Property(e => e.WindowsTg).HasColumnName("EcoWindowsTg").HasMaxLength(200);
            eco.Property(e => e.WallInsulationEn).HasColumnName("EcoWallInsulationEn").HasMaxLength(200);
            eco.Property(e => e.WindowsEn).HasColumnName("EcoWindowsEn").HasMaxLength(200);
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
