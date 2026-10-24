using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class PaymentPlanConfiguration : IEntityTypeConfiguration<PaymentPlan>
{
    public void Configure(EntityTypeBuilder<PaymentPlan> builder)
    {
        builder.ToTable("PaymentPlans");
        builder.Property(p => p.DownPaymentPct).HasPrecision(5, 2);
        builder.Ignore(p => p.Total);
        builder.HasIndex(p => p.DealId).IsUnique();
        builder.HasOne(p => p.Deal).WithOne().HasForeignKey<PaymentPlan>(p => p.DealId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Items).WithOne(i => i.Plan).HasForeignKey(i => i.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentScheduleItemConfiguration : IEntityTypeConfiguration<PaymentScheduleItem>
{
    public void Configure(EntityTypeBuilder<PaymentScheduleItem> builder)
    {
        builder.ToTable("PaymentScheduleItems");
        builder.HasIndex(i => new { i.PlanId, i.DueDate });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.Property(p => p.RateUsed).HasPrecision(18, 4);
        builder.Property(p => p.Comment).HasMaxLength(500);
        builder.Ignore(p => p.IsReversal);
        builder.HasIndex(p => new { p.DealId, p.PaidAt });
        // Один платёж можно сторнировать только один раз.
        builder.HasIndex(p => p.ReversalOfId).IsUnique().HasFilter("\"ReversalOfId\" IS NOT NULL");
        builder.HasOne(p => p.Deal).WithMany().HasForeignKey(p => p.DealId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ReversalOf).WithMany().HasForeignKey(p => p.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.Property(r => r.RateToTjs).HasPrecision(18, 4);
        builder.HasIndex(r => new { r.Currency, r.Date }).IsUnique();
    }
}

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.Property(d => d.FileKey).HasMaxLength(300).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.HasIndex(d => d.DealId);
        builder.HasOne(d => d.Deal).WithMany().HasForeignKey(d => d.DealId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SmsCodeConfiguration : IEntityTypeConfiguration<SmsCode>
{
    public void Configure(EntityTypeBuilder<SmsCode> builder)
    {
        builder.ToTable("SmsCodes");
        builder.Property(c => c.Phone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(c => new { c.Phone, c.CreatedAt });
    }
}
