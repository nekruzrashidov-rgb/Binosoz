using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");
        builder.Property(a => a.Entity).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Before).HasColumnType("jsonb");
        builder.Property(a => a.After).HasColumnType("jsonb");
        builder.HasIndex(a => new { a.Entity, a.EntityId });
        builder.HasIndex(a => a.At);
        builder.HasIndex(a => new { a.OrganizationId, a.At });
        // Без внешнего ключа на Users: аудит должен переживать удаление пользователя.
    }
}
