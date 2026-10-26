using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(64).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();
        builder.Property(o => o.LogoUrl).HasMaxLength(500);
        builder.Property(o => o.LogoFileKey).HasMaxLength(300);
        builder.Property(o => o.LogoContentType).HasMaxLength(100);
        builder.Property(o => o.PrimaryColor).HasMaxLength(7);
        builder.Property(o => o.Phone).HasMaxLength(32);
        builder.Property(o => o.Telegram).HasMaxLength(64);
        builder.Property(o => o.Address).HasMaxLength(300);
        builder.Property(o => o.CustomDomain).HasMaxLength(253);
        builder.HasIndex(o => o.CustomDomain).IsUnique().HasFilter("\"CustomDomain\" IS NOT NULL");
    }
}

public class UserBuildingConfiguration : IEntityTypeConfiguration<UserBuilding>
{
    public void Configure(EntityTypeBuilder<UserBuilding> builder)
    {
        builder.ToTable("UserBuildings");
        builder.HasKey(x => new { x.UserId, x.BuildingId });
        builder.HasOne(x => x.User).WithMany(u => u.Buildings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailCodeConfiguration : IEntityTypeConfiguration<EmailCode>
{
    public void Configure(EntityTypeBuilder<EmailCode> builder)
    {
        builder.ToTable("EmailCodes");
        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.RequestIp).HasMaxLength(64);
        builder.HasIndex(c => new { c.Email, c.CreatedAt });
        builder.HasIndex(c => new { c.RequestIp, c.CreatedAt });
    }
}

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.FullName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Role).HasMaxLength(32).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.OrganizationId, i.Email });
        builder.HasOne(i => i.Organization).WithMany().HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}
