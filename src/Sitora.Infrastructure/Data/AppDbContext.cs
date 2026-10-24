using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<PaymentPlan> PaymentPlans => Set<PaymentPlan>();
    public DbSet<PaymentScheduleItem> PaymentScheduleItems => Set<PaymentScheduleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<SmsCode> SmsCodes => Set<SmsCode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Перечисления храним строками: так данные читаются в SQL и работают частичные индексы по статусу.
        configurationBuilder.Properties<ApartmentStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Currency>().HaveConversion<string>().HaveMaxLength(3);
        configurationBuilder.Properties<LeadStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<LeadSource>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<BookingStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<DealStage>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ScheduleItemKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ScheduleItemStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<RateSource>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<DocumentType>().HaveConversion<string>().HaveMaxLength(32);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void StampTimestamps()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
