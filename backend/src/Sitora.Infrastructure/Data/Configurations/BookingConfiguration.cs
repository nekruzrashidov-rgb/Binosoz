using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public const string ActiveBookingIndex = "IX_Bookings_ApartmentId_Active";

    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", t => t.HasCheckConstraint(
            "CK_Bookings_CustomerOrLead", "\"CustomerId\" IS NOT NULL OR \"LeadId\" IS NOT NULL"));
        builder.Property(b => b.CancelReason).HasMaxLength(500);

        // Вторая защита от двойной брони, кроме блокировки строки квартиры в транзакции.
        builder.HasIndex(b => b.ApartmentId)
            .HasDatabaseName(ActiveBookingIndex)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");
        builder.HasIndex(b => new { b.Status, b.ExpiresAt });

        builder.HasOne(b => b.Apartment).WithMany().HasForeignKey(b => b.ApartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Customer).WithMany().HasForeignKey(b => b.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Lead).WithMany().HasForeignKey(b => b.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}
