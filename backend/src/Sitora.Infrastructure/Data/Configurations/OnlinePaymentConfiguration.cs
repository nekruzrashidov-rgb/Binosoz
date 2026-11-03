using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class OnlinePaymentConfiguration : IEntityTypeConfiguration<OnlinePayment>
{
    public void Configure(EntityTypeBuilder<OnlinePayment> builder)
    {
        builder.ToTable("OnlinePayments");
        builder.Property(p => p.Provider).HasMaxLength(20).IsRequired();
        builder.Property(p => p.GatewayPaymentId).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(p => new { p.Provider, p.GatewayPaymentId }).IsUnique();
        builder.HasIndex(p => p.DealId);
        builder.HasOne(p => p.Deal).WithMany().HasForeignKey(p => p.DealId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Payment).WithMany().HasForeignKey(p => p.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
