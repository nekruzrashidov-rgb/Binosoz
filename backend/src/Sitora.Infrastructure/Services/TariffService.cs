using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Тарифы платформы и лимиты по ним. Действующий тариф организации — последний начавшийся и не закончившийся.
// У организации без тарифа лимитов нет: так работают организации, созданные до тарифов.
public class TariffService(AppDbContext db, IAuditService audit, TimeProvider timeProvider) : ITariffService
{
    private const string TariffNotFound = "Тариф не найден";
    private const int MaxFeatures = 20;

    public async Task<IReadOnlyList<TariffPlanResponse>> GetAsync(CancellationToken ct = default)
    {
        var now = Now;
        var plans = await db.TariffPlans.AsNoTracking().OrderBy(t => t.Price).ThenBy(t => t.Name).ToListAsync(ct);
        var counts = await db.OrganizationTariffs.AsNoTracking()
            .Where(t => t.StartsAt <= now && (t.EndsAt == null || t.EndsAt > now))
            .GroupBy(t => t.TariffPlanId).Select(g => new { g.Key, Count = g.Select(t => t.OrganizationId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return plans.Select(p => ToResponse(p, counts.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<TariffPlanResponse> CreateAsync(SaveTariffRequest request, CancellationToken ct = default)
    {
        var plan = new TariffPlan();
        await ApplyAsync(plan, request, ct);
        db.TariffPlans.Add(plan);
        audit.Record(nameof(TariffPlan), plan.Id, "Created", null, Snapshot(plan));
        await db.SaveChangesAsync(ct);
        return ToResponse(plan, 0);
    }

    public async Task<TariffPlanResponse> UpdateAsync(Guid id, SaveTariffRequest request, CancellationToken ct = default)
    {
        var plan = await db.TariffPlans.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(TariffNotFound);
        var before = Snapshot(plan);
        await ApplyAsync(plan, request, ct);
        audit.Record(nameof(TariffPlan), plan.Id, "Updated", before, Snapshot(plan));
        await db.SaveChangesAsync(ct);
        return (await GetAsync(ct)).Single(t => t.Id == id);
    }

    public async Task<OrganizationTariffResponse> AssignAsync(Guid organizationId, AssignTariffRequest request, CancellationToken ct = default)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
        var plan = await db.TariffPlans.FirstOrDefaultAsync(t => t.Id == request.TariffPlanId, ct) ?? throw new NotFoundException(TariffNotFound);
        if (!plan.IsActive)
            throw new BusinessRuleException("Тариф отключён: назначить его нельзя");
        var now = Now;
        if (request.EndsAt is { } endsAt && endsAt <= now)
            throw new BusinessRuleException("Дата окончания тарифа должна быть в будущем");

        // Прежний действующий тариф заканчивается сейчас: в один момент у организации один тариф.
        var current = await db.OrganizationTariffs
            .Where(t => t.OrganizationId == organizationId && t.StartsAt <= now && (t.EndsAt == null || t.EndsAt > now))
            .ToListAsync(ct);
        current.ForEach(t => t.EndsAt = now);

        var assigned = new OrganizationTariff
        {
            OrganizationId = organizationId,
            TariffPlanId = plan.Id,
            StartsAt = now,
            EndsAt = request.EndsAt?.ToUniversalTime(),
            IsPaid = request.IsPaid
        };
        db.OrganizationTariffs.Add(assigned);
        audit.Record(nameof(Organization), organization.Id, "TariffAssigned",
            current.Count > 0 ? new { TariffPlanIds = current.Select(t => t.TariffPlanId) } : null,
            new { TariffPlanId = plan.Id, plan.Name, assigned.EndsAt, assigned.IsPaid }, organization.Id);
        await db.SaveChangesAsync(ct);
        return ToResponse(assigned, plan);
    }

    public async Task<IReadOnlyDictionary<Guid, OrganizationTariffResponse>> CurrentAsync(IReadOnlyCollection<Guid> organizationIds,
        CancellationToken ct = default)
    {
        var now = Now;
        var rows = await db.OrganizationTariffs.AsNoTracking().Include(t => t.TariffPlan)
            .Where(t => organizationIds.Contains(t.OrganizationId) && t.StartsAt <= now && (t.EndsAt == null || t.EndsAt > now))
            .ToListAsync(ct);
        return rows.GroupBy(t => t.OrganizationId)
            .ToDictionary(g => g.Key, g => ToResponse(g.OrderByDescending(t => t.StartsAt).First(), g.First().TariffPlan));
    }

    public async Task EnsureCanAddBuildingAsync(Guid organizationId, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(organizationId, ct);
        if (plan?.MaxBuildings is not { } max)
            return;
        var buildings = await db.Buildings.IgnoreQueryFilters().CountAsync(b => b.OrganizationId == organizationId, ct);
        if (buildings >= max)
            throw new PaymentRequiredException($"Тариф «{plan.Name}» позволяет не больше {max} {Plural(max, "дома", "домов", "домов")}. Перейдите на тариф выше");
    }

    public async Task EnsureCanAddUserAsync(Guid organizationId, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(organizationId, ct);
        if (plan?.MaxUsers is not { } max)
            return;
        var users = await db.Users.CountAsync(u => u.OrganizationId == organizationId && u.IsActive, ct);
        if (users >= max)
            throw new PaymentRequiredException($"Тариф «{plan.Name}» позволяет не больше {max} {Plural(max, "сотрудника", "сотрудников", "сотрудников")}. Перейдите на тариф выше");
    }

    private async Task<TariffPlan?> CurrentPlanAsync(Guid organizationId, CancellationToken ct)
    {
        var now = Now;
        return await db.OrganizationTariffs.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.StartsAt <= now && (t.EndsAt == null || t.EndsAt > now))
            .OrderByDescending(t => t.StartsAt)
            .Select(t => t.TariffPlan)
            .FirstOrDefaultAsync(ct);
    }

    private async Task ApplyAsync(TariffPlan plan, SaveTariffRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 50)
            throw new BusinessRuleException("Название тарифа — от 1 до 50 символов");
        if (request.Price is < 0 or > 1_000_000_000)
            throw new BusinessRuleException("Цена тарифа — от 0 до 1 000 000 000");
        if (request.MaxBuildings is < 1 || request.MaxUsers is < 1)
            throw new BusinessRuleException("Лимит — не меньше 1; пусто — без ограничения");
        var features = (request.Features ?? []).Select(f => f?.Trim() ?? "").Where(f => f.Length > 0).ToList();
        if (features.Count > MaxFeatures || features.Any(f => f.Length > 200))
            throw new BusinessRuleException($"Возможностей тарифа — не больше {MaxFeatures}, каждая до 200 символов");
        if (await db.TariffPlans.AnyAsync(t => t.Name == name && t.Id != plan.Id, ct))
            throw new ConflictException("Тариф с таким названием уже есть");

        plan.Name = name;
        plan.Price = request.Price;
        plan.Currency = request.Currency;
        plan.MaxBuildings = request.MaxBuildings;
        plan.MaxUsers = request.MaxUsers;
        plan.Features = JsonSerializer.Serialize(features);
        plan.IsActive = request.IsActive;
    }

    private static object Snapshot(TariffPlan p) => new { p.Name, p.Price, p.Currency, p.MaxBuildings, p.MaxUsers, p.Features, p.IsActive };

    public static IReadOnlyList<string> ParseFeatures(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static TariffPlanResponse ToResponse(TariffPlan p, int organizations) =>
        new(p.Id, p.Name, p.Price, p.Currency, p.MaxBuildings, p.MaxUsers, ParseFeatures(p.Features), p.IsActive, organizations);

    private static OrganizationTariffResponse ToResponse(OrganizationTariff t, TariffPlan p) =>
        new(t.Id, t.OrganizationId, p.Id, p.Name, p.Price, p.Currency, t.StartsAt, t.EndsAt, t.IsPaid);

    private static string Plural(int n, string one, string few, string many) =>
        (n % 10, n % 100) switch
        {
            (1, not 11) => one,
            (2 or 3 or 4, not (12 or 13 or 14)) => few,
            _ => many
        };

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
