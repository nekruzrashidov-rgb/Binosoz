using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class BuyerMessageConfiguration : IEntityTypeConfiguration<BuyerMessage>
{
    public void Configure(EntityTypeBuilder<BuyerMessage> builder)
    {
        builder.ToTable("BuyerMessages");
        builder.Property(m => m.Event).HasMaxLength(32).IsRequired();
        builder.Property(m => m.Channel).HasMaxLength(16).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(16).IsRequired();
        builder.Property(m => m.Error).HasMaxLength(500);
        builder.Property(m => m.Subject).HasMaxLength(300);
        builder.Property(m => m.Text).HasMaxLength(4000).IsRequired();
        builder.Property(m => m.DedupeKey).HasMaxLength(200);
        // Одно напоминание одного события по одному каналу на пункт графика: повторный запуск задачи ничего не шлёт.
        builder.HasIndex(m => m.DedupeKey).IsUnique().HasFilter("\"DedupeKey\" IS NOT NULL");
        builder.HasIndex(m => new { m.DealId, m.CreatedAt });
        builder.HasIndex(m => new { m.OrganizationId, m.CreatedAt });
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Deal>().WithMany().HasForeignKey(m => m.DealId).OnDelete(DeleteBehavior.Cascade);
    }
}
