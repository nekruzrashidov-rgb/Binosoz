using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data;

// currentUser == null — контекст без пользователя (миграции, сидеры в тестах): ограничений нет.
public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider, ICurrentUser? currentUser = null)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    // Параметры фильтров доступа. EF читает их у текущего экземпляра контекста при каждом запросе.
    public bool ScopeAll => currentUser?.OrganizationId is null;
    public Guid ScopeOrganizationId => currentUser?.OrganizationId ?? Guid.Empty;
    public bool ScopeAllBuildings => currentUser?.BuildingIds is null;
    public Guid[] ScopeBuildingIds => currentUser?.BuildingIds?.ToArray() ?? [];

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserBuilding> UserBuildings => Set<UserBuilding>();
    public DbSet<EmailCode> EmailCodes => Set<EmailCode>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OrgApplication> OrgApplications => Set<OrgApplication>();
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
    public DbSet<ConstructionStage> ConstructionStages => Set<ConstructionStage>();
    public DbSet<ConstructionPhoto> ConstructionPhotos => Set<ConstructionPhoto>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

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
        ApplyAccessFilters(builder);
    }

    // Мультиарендность на уровне запросов к базе: сотрудник видит только данные своей организации
    // и своих домов, что бы ни делал контроллер или сервис. Для каждой сущности задаётся путь
    // до организации и до дома. Обойти фильтр можно только явным IgnoreQueryFilters().
    private void ApplyAccessFilters(ModelBuilder builder)
    {
        ByBuilding<Building>(builder, x => x.OrganizationId, x => x.Id);
        ByBuilding<Floor>(builder, x => x.Building.OrganizationId, x => x.BuildingId);
        ByBuilding<Apartment>(builder, x => x.Floor.Building.OrganizationId, x => x.Floor.BuildingId);
        ByBuilding<Booking>(builder, x => x.Apartment.Floor.Building.OrganizationId, x => x.Apartment.Floor.BuildingId);
        ByBuilding<Deal>(builder, x => x.Apartment.Floor.Building.OrganizationId, x => x.Apartment.Floor.BuildingId);
        ByBuilding<PaymentPlan>(builder, x => x.Deal.Apartment.Floor.Building.OrganizationId, x => x.Deal.Apartment.Floor.BuildingId);
        ByBuilding<PaymentScheduleItem>(builder, x => x.Plan.Deal.Apartment.Floor.Building.OrganizationId, x => x.Plan.Deal.Apartment.Floor.BuildingId);
        ByBuilding<Payment>(builder, x => x.Deal.Apartment.Floor.Building.OrganizationId, x => x.Deal.Apartment.Floor.BuildingId);
        ByBuilding<Document>(builder, x => x.Deal.Apartment.Floor.Building.OrganizationId, x => x.Deal.Apartment.Floor.BuildingId);
        ByBuilding<ChatMessage>(builder, x => x.Deal.Apartment.Floor.Building.OrganizationId, x => x.Deal.Apartment.Floor.BuildingId);
        ByBuilding<ConstructionStage>(builder, x => x.Building.OrganizationId, x => x.BuildingId);
        ByBuilding<ConstructionPhoto>(builder, x => x.Stage.Building.OrganizationId, x => x.Stage.BuildingId);

        // Заявка без дома видна только тем, у кого доступ ко всем домам организации.
        builder.Entity<Lead>().HasQueryFilter(x => ScopeAll || (x.OrganizationId == ScopeOrganizationId
            && (ScopeAllBuildings || (x.BuildingId != null && ScopeBuildingIds.Contains(x.BuildingId.Value)))));

        // Клиента сотрудник с частью домов видит, если клиента завели в его доме или у клиента там сделка.
        builder.Entity<Customer>().HasQueryFilter(x => ScopeAll || (x.OrganizationId == ScopeOrganizationId
            && (ScopeAllBuildings
                || (x.HomeBuildingId != null && ScopeBuildingIds.Contains(x.HomeBuildingId.Value))
                || x.Deals.Any(d => ScopeBuildingIds.Contains(d.Apartment.Floor.BuildingId)))));

        builder.Entity<AuditLog>().HasQueryFilter(x => ScopeAll || x.OrganizationId == ScopeOrganizationId);
        builder.Entity<Invitation>().HasQueryFilter(x => ScopeAll || x.OrganizationId == ScopeOrganizationId);
        builder.Entity<Organization>().HasQueryFilter(x => ScopeAll || x.Id == ScopeOrganizationId);
    }

    private void ByBuilding<T>(ModelBuilder builder, Expression<Func<T, Guid>> organizationId, Expression<Func<T, Guid>> buildingId)
        where T : class
    {
        Expression<Func<Guid, Guid, bool>> rule = (org, building) =>
            ScopeAll || (org == ScopeOrganizationId && (ScopeAllBuildings || ScopeBuildingIds.Contains(building)));

        var entity = organizationId.Parameters[0];
        var body = ReplacingExpressionVisitor.Replace(
            [rule.Parameters[0], rule.Parameters[1]],
            [organizationId.Body, ReplacingExpressionVisitor.Replace(buildingId.Parameters[0], entity, buildingId.Body)],
            rule.Body);
        builder.Entity<T>().HasQueryFilter(Expression.Lambda<Func<T, bool>>(body, entity));
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
