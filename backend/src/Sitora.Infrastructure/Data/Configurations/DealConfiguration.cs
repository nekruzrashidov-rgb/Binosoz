using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public const string OpenDealIndex = "IX_Deals_ApartmentId_Open";

    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("Deals");
        builder.Property(d => d.StageReason).HasMaxLength(500);
        builder.Property(d => d.DiscountReason).HasMaxLength(500);
        builder.Ignore(d => d.FinalPrice);
        builder.Ignore(d => d.IsClosed);

        // На квартиру может быть только одна неотменённая сделка.
        builder.HasIndex(d => d.ApartmentId)
            .HasDatabaseName(OpenDealIndex)
            .IsUnique()
            .HasFilter("\"Stage\" <> 'Cancelled'");
        builder.HasIndex(d => d.Stage);
        builder.HasIndex(d => d.CustomerId);

        builder.HasOne(d => d.Apartment).WithMany().HasForeignKey(d => d.ApartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Customer).WithMany(c => c.Deals).HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Manager).WithMany().HasForeignKey(d => d.ManagerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Booking).WithMany().HasForeignKey(d => d.BookingId).OnDelete(DeleteBehavior.SetNull);
    }
}
