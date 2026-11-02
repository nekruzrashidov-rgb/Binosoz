using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.ToTable("CashTransactions");
        builder.Property(x => x.RateUsed).HasPrecision(18, 4);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Ignore(x => x.IsReversal);

        builder.HasIndex(x => new { x.BuildingId, x.OperationDate });
        // Одну операцию можно сторнировать только один раз.
        builder.HasIndex(x => x.ReversalOfId).IsUnique().HasFilter("\"ReversalOfId\" IS NOT NULL");

        builder.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Deal).WithMany().HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversalOf).WithMany().HasForeignKey(x => x.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WeeklyReportConfiguration : IEntityTypeConfiguration<WeeklyReport>
{
    public void Configure(EntityTypeBuilder<WeeklyReport> builder)
    {
        builder.ToTable("WeeklyReports");
        // Один отчёт на дом и неделю.
        builder.HasIndex(x => new { x.BuildingId, x.PeriodStart }).IsUnique();
        builder.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
    }
}
