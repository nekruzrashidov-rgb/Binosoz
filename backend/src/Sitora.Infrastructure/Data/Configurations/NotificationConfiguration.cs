using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Channel).HasMaxLength(32).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200);
        builder.Property(n => n.Text).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.Kind).HasMaxLength(32);
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        // Одно напоминание одного вида на пункт графика (сделка + срок) каждому получателю.
        builder.HasIndex(n => new { n.UserId, n.Kind, n.DealId, n.DueDate }).IsUnique().HasFilter("\"Kind\" IS NOT NULL");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Deal>().WithMany().HasForeignKey(n => n.DealId).OnDelete(DeleteBehavior.Cascade);
    }
}
