using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class ConstructionStageConfiguration : IEntityTypeConfiguration<ConstructionStage>
{
    public void Configure(EntityTypeBuilder<ConstructionStage> builder)
    {
        builder.ToTable("ConstructionStages", t =>
            t.HasCheckConstraint("CK_ConstructionStages_Percent", "\"Percent\" BETWEEN 0 AND 100"));
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => new { s.BuildingId, s.SortOrder });
        builder.HasOne(s => s.Building).WithMany().HasForeignKey(s => s.BuildingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Photos).WithOne(p => p.Stage).HasForeignKey(p => p.StageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConstructionPhotoConfiguration : IEntityTypeConfiguration<ConstructionPhoto>
{
    public void Configure(EntityTypeBuilder<ConstructionPhoto> builder)
    {
        builder.ToTable("ConstructionPhotos");
        builder.Property(p => p.FileKey).HasMaxLength(300).IsRequired();
        builder.Property(p => p.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Caption).HasMaxLength(500);
        builder.HasIndex(p => new { p.StageId, p.TakenAt });
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.Property(m => m.Text).HasMaxLength(ChatMessage.MaxLength).IsRequired();
        builder.HasIndex(m => new { m.DealId, m.CreatedAt });
        builder.HasOne(m => m.Deal).WithMany().HasForeignKey(m => m.DealId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
