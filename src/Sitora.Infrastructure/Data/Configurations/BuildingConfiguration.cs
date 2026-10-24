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
    }
}
