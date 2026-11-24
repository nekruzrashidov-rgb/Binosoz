using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Data.Seed;

namespace Sitora.Infrastructure.Services;

// Кто и над чем — понятно человеку (C-06 / R-04): автор записи — пользователь из UserId, а если его нет и объект —
// сам пользователь (вход, регистрация, сброс пароля до входа), то он и есть автор. «Система» — только сидер и фоновые
// задачи. Объект — имя или номер квартиры вместо «AppUser #5C086CDE». Тексты действий переводит сайт (audit.act.*).
public sealed record AuditSubject(Guid? ActorId, string? ActorName, string? ActorEmail, bool BySystem,
    string? EntityName, int? ApartmentNumber);

public static class AuditPresenter
{
    // Действия, которые совершает не человек, даже если объект записи — пользователь: сидер и фоновые задачи Hangfire.
    public static readonly IReadOnlySet<string> SystemActions = new HashSet<string>(StringComparer.Ordinal)
    {
        SeedPasswords.PasswordSetAction, SeedPasswords.CreatedAction, DatabaseInitializer.NameSetAction,
        "Seeded", "DemoSeeded", "DemoBookingsRenewed", "Expired", "PinLockedOut"
    };

    public sealed record Row(Guid Id, Guid? UserId, string Entity, Guid EntityId, string Action);

    private static bool IsUserEntity(string entity) => entity is nameof(AppUser) or "User";

    public static async Task<Dictionary<Guid, AuditSubject>> DescribeAsync(AppDbContext db, IReadOnlyCollection<Row> rows,
        CancellationToken ct = default)
    {
        Guid? ActorOf(Row r) => r.UserId ?? (IsUserEntity(r.Entity) && !SystemActions.Contains(r.Action) ? r.EntityId : null);

        Guid[] Ids(params string[] entities) => rows.Where(r => entities.Contains(r.Entity)).Select(r => r.EntityId).Distinct().ToArray();

        // Пользователи — без фильтров организации: автор записи в журнале организации может быть создателем платформы
        // («войти как владелец»), и без имени он выглядел бы как «система».
        var userIds = rows.Select(ActorOf).OfType<Guid>().Concat(Ids(nameof(AppUser), "User")).Distinct().ToArray();
        var users = userIds.Length == 0 ? [] : await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);

        var names = new Dictionary<Guid, string>();
        var apartments = new Dictionary<Guid, int>();

        var dealIds = Ids(nameof(Deal));
        if (dealIds.Length > 0)
            foreach (var d in await db.Deals.AsNoTracking().Where(d => dealIds.Contains(d.Id))
                         .Select(d => new { d.Id, d.Apartment.Number, d.Customer.FullName }).ToListAsync(ct))
            {
                apartments[d.Id] = d.Number;
                names[d.Id] = d.FullName;
            }

        var paymentIds = Ids(nameof(Payment));
        if (paymentIds.Length > 0)
            foreach (var p in await db.Payments.AsNoTracking().Where(p => paymentIds.Contains(p.Id))
                         .Select(p => new { p.Id, p.Deal.Apartment.Number, p.Deal.Customer.FullName }).ToListAsync(ct))
            {
                apartments[p.Id] = p.Number;
                names[p.Id] = p.FullName;
            }

        var bookingIds = Ids(nameof(Booking));
        if (bookingIds.Length > 0)
            foreach (var b in await db.Bookings.AsNoTracking().Where(b => bookingIds.Contains(b.Id))
                         .Select(b => new { b.Id, b.Apartment.Number, Name = b.Customer != null ? b.Customer.FullName : b.Lead != null ? b.Lead.Name : null })
                         .ToListAsync(ct))
            {
                apartments[b.Id] = b.Number;
                if (b.Name is not null)
                    names[b.Id] = b.Name;
            }

        var apartmentIds = Ids(nameof(Apartment));
        if (apartmentIds.Length > 0)
            foreach (var a in await db.Apartments.AsNoTracking().Where(a => apartmentIds.Contains(a.Id))
                         .Select(a => new { a.Id, a.Number }).ToListAsync(ct))
                apartments[a.Id] = a.Number;


        var leadIds = Ids(nameof(Lead));
        if (leadIds.Length > 0)
            foreach (var l in await db.Leads.AsNoTracking().Where(l => leadIds.Contains(l.Id)).Select(l => new { l.Id, l.Name }).ToListAsync(ct))
                names[l.Id] = l.Name;
        var customerIds = Ids(nameof(Customer));
        if (customerIds.Length > 0)
            foreach (var c in await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).Select(c => new { c.Id, c.FullName }).ToListAsync(ct))
                names[c.Id] = c.FullName;
        var buildingIds = Ids(nameof(Building));
        if (buildingIds.Length > 0)
            foreach (var b in await db.Buildings.AsNoTracking().Where(b => buildingIds.Contains(b.Id)).Select(b => new { b.Id, b.NameRu }).ToListAsync(ct))
                names[b.Id] = b.NameRu;
        var organizationIds = Ids(nameof(Organization), "Impersonation");
        if (organizationIds.Length > 0)
            foreach (var o in await db.Organizations.AsNoTracking().Where(o => organizationIds.Contains(o.Id)).Select(o => new { o.Id, o.Name }).ToListAsync(ct))
                names[o.Id] = o.Name;
        var invitationIds = Ids(nameof(Invitation));
        if (invitationIds.Length > 0)
            foreach (var i in await db.Invitations.AsNoTracking().Where(i => invitationIds.Contains(i.Id)).Select(i => new { i.Id, i.Email, i.FullName }).ToListAsync(ct))
                names[i.Id] = string.IsNullOrWhiteSpace(i.FullName) ? i.Email : $"{i.FullName} ({i.Email})";


        var result = new Dictionary<Guid, AuditSubject>(rows.Count);
        foreach (var r in rows)
        {
            var actorId = ActorOf(r);
            var actor = actorId is { } a && users.TryGetValue(a, out var u) ? u : null;
            string? entityName = null;
            if (IsUserEntity(r.Entity) && users.TryGetValue(r.EntityId, out var subject))
                entityName = string.IsNullOrWhiteSpace(subject.FullName) ? subject.Email : subject.FullName;
            else if (names.TryGetValue(r.EntityId, out var name))
                entityName = name;
            result[r.Id] = new AuditSubject(actorId, actor?.FullName, actor?.Email, actorId is null, entityName,
                apartments.TryGetValue(r.EntityId, out var number) ? number : null);
        }
        return result;
    }
}
