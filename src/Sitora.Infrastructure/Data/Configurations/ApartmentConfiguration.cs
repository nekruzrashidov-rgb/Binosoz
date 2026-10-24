using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("Apartments");
        builder.HasIndex(a => new { a.FloorId, a.Number }).IsUnique();
        builder.HasIndex(a => a.Status);
        builder.HasOne(a => a.Floor)
            .WithMany(f => f.Apartments)
            .HasForeignKey(a => a.FloorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
