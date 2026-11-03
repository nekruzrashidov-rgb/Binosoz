using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        builder.Property(r => r.Text).HasMaxLength(Review.MaxTextLength).IsRequired();
        builder.Property(r => r.OwnerReply).HasMaxLength(2000);
        builder.Property(r => r.AiReply).HasMaxLength(2000);
        // Один отзыв на покупателя и дом.
        builder.HasIndex(r => new { r.BuildingId, r.CustomerId }).IsUnique();
        builder.HasIndex(r => new { r.BuildingId, r.IsPublished });
        builder.HasOne(r => r.Building).WithMany().HasForeignKey(r => r.BuildingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
