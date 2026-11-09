using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Разделы суперадмина «Активность платформы», «Уведомления» и «Настройки платформы».
// Контроллер пускает только SuperAdmin; суперадмин видит данные всех организаций (фильтры AppDbContext его не ограничивают).
public class PlatformToolsService(AppDbContext db, IAuditService audit, TimeProvider time) : IPlatformToolsService
{
    public static readonly string[] NotificationTypes = ["info", "warning", "error"];
    private const int MaxTitle = 200;
    private const int MaxMessage = 2000;

    public async Task<PagedResult<PlatformActivityItem>> ActivityAsync(PlatformActivityQuery query, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(query.Page, query.PageSize);
        var logs = db.AuditLogs.AsNoTracking();
        if (query.OrganizationId is { } organizationId)
            logs = logs.Where(a => a.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(query.Action))
            logs = logs.Where(a => a.Action == query.Action.Trim());
        if (query.From is { } from)
            logs = logs.Where(a => a.At >= from);
        if (query.To is { } to)
            logs = logs.Where(a => a.At < to);

        var total = await logs.CountAsync(ct);
        var items = await (
                from a in logs
                join u in db.Users on a.UserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                join o in db.Organizations.IgnoreQueryFilters() on a.OrganizationId equals o.Id into orgs
                from o in orgs.DefaultIfEmpty()
                orderby a.At descending, a.Id descending
                // Вход и другие действия до аутентификации пишутся без UserId: тогда пользователь — сам объект записи (AppUser).
                select new PlatformActivityItem(a.Id, a.At, a.Action, a.Entity, a.EntityId, a.OrganizationId,
                    o != null ? o.Name : null,
                    u != null ? u.Email
                        : a.Entity == nameof(AppUser) ? db.Users.Where(x => x.Id == a.EntityId).Select(x => x.Email).FirstOrDefault() : null,
                    a.Ip, a.ImpersonatedById != null))
            .Skip(p.Skip).Take(p.PageSize)
            .ToListAsync(ct);
        return new PagedResult<PlatformActivityItem>(items, p.Page, p.PageSize, total);
    }

    // Действия для фильтра: те, что уже встречаются в журнале.
    public async Task<IReadOnlyList<string>> ActivityActionsAsync(CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);

    // Уведомление в колокольчик каждому пользователю организации (или всех организаций). Покупатели без организации не получают.
    public async Task<PlatformNotificationResponse> SendNotificationAsync(PlatformNotificationRequest request, CancellationToken ct = default)
    {
        var type = request.Type?.Trim().ToLowerInvariant() ?? "";
        if (!NotificationTypes.Contains(type))
            throw new BusinessRuleException("Тип уведомления — информация, предупреждение или ошибка");
        var title = request.Title?.Trim() ?? "";
        var message = request.Message?.Trim() ?? "";
        if (title.Length == 0)
            throw new BusinessRuleException("Введите заголовок уведомления");
        if (title.Length > MaxTitle)
            throw new BusinessRuleException($"Заголовок — не длиннее {MaxTitle} символов");
        if (message.Length == 0)
            throw new BusinessRuleException("Введите текст уведомления");
        if (message.Length > MaxMessage)
            throw new BusinessRuleException($"Текст — не длиннее {MaxMessage} символов");
        if (request.OrganizationId is { } id && !await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Id == id, ct))
            throw new NotFoundException("Организация не найдена");

        var users = db.Users.Where(u => u.IsActive && u.OrganizationId != null);
        if (request.OrganizationId is { } organizationId)
            users = users.Where(u => u.OrganizationId == organizationId);
        var userIds = await users.Select(u => u.Id).ToListAsync(ct);

        var now = time.GetUtcNow().UtcDateTime;
        db.Notifications.AddRange(userIds.Select(userId => new Notification
        {
            UserId = userId,
            Channel = Notification.InAppChannel,
            Kind = "platform_" + type,
            Title = title,
            Text = message,
            SentAt = now
        }));
        audit.Record("PlatformNotification", Guid.CreateVersion7(), "Sent", null,
            new { request.OrganizationId, Type = type, Title = title, Recipients = userIds.Count }, request.OrganizationId);
        await db.SaveChangesAsync(ct);
        return new PlatformNotificationResponse(userIds.Count);
    }

    public async Task<PlatformSettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        var values = await db.PlatformSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var organizations = await db.Organizations.IgnoreQueryFilters().CountAsync(ct);
        return new PlatformSettingsResponse(
            values.TryGetValue(PlatformSettingKeys.MaxOrganizations, out var max) && int.TryParse(max, NumberStyles.None, CultureInfo.InvariantCulture, out var m) ? m : null,
            values.TryGetValue(PlatformSettingKeys.DemoMode, out var demo) && demo == "true",
            Version,
            values.GetValueOrDefault(PlatformSettingKeys.ContactEmail),
            organizations);
    }

    public async Task<PlatformSettingsResponse> UpdateSettingsAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default)
    {
        var before = await GetSettingsAsync(ct);
        if (request.ClearMaxOrganizations == true)
            await RemoveAsync(PlatformSettingKeys.MaxOrganizations, ct);
        else if (request.MaxOrganizations is { } max)
        {
            if (max < 1 || max > 100_000)
                throw new BusinessRuleException("Максимум организаций — от 1 до 100 000");
            if (max < before.Organizations)
                throw new BusinessRuleException($"Организаций на платформе сейчас: {before.Organizations}. Лимит не может быть меньше");
            await SetAsync(PlatformSettingKeys.MaxOrganizations, max.ToString(CultureInfo.InvariantCulture), ct);
        }
        if (request.DemoMode is { } demo)
            await SetAsync(PlatformSettingKeys.DemoMode, demo ? "true" : "false", ct);
        if (request.ContactEmail is { } email)
        {
            if (string.IsNullOrWhiteSpace(email))
                await RemoveAsync(PlatformSettingKeys.ContactEmail, ct);
            else
                await SetAsync(PlatformSettingKeys.ContactEmail,
                    EmailAddress.Normalize(email) ?? throw new BusinessRuleException("Неверный адрес почты"), ct);
        }

        var after = before with
        {
            MaxOrganizations = request.ClearMaxOrganizations == true ? null : request.MaxOrganizations ?? before.MaxOrganizations,
            DemoMode = request.DemoMode ?? before.DemoMode,
            ContactEmail = request.ContactEmail is null ? before.ContactEmail
                : string.IsNullOrWhiteSpace(request.ContactEmail) ? null : EmailAddress.Normalize(request.ContactEmail)
        };
        audit.Record("PlatformSettings", Guid.Empty, "Updated",
            new { before.MaxOrganizations, before.DemoMode, before.ContactEmail },
            new { after.MaxOrganizations, after.DemoMode, after.ContactEmail });
        await db.SaveChangesAsync(ct);
        return after;
    }

    public async Task EnsureOrganizationLimitAsync(CancellationToken ct = default)
    {
        var value = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == PlatformSettingKeys.MaxOrganizations).Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var max))
            return;
        if (await db.Organizations.IgnoreQueryFilters().CountAsync(ct) >= max)
            throw new BusinessRuleException($"Достигнут лимит организаций платформы ({max}). Увеличьте его в настройках платформы");
    }

    private async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var setting = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
            db.PlatformSettings.Add(setting = new PlatformSetting { Key = key });
        setting.Value = value;
        setting.UpdatedAt = time.GetUtcNow().UtcDateTime;
    }

    private async Task RemoveAsync(string key, CancellationToken ct)
    {
        if (await db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct) is { } setting)
            db.PlatformSettings.Remove(setting);
    }

    // «1.0.0 (4ac525d)»: версия сборки и коммит, из которого она собрана.
    private static readonly string Version = ReadVersion();

    private static string ReadVersion()
    {
        var info = typeof(PlatformToolsService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var plus = info.IndexOf('+');
        return plus < 0 ? info : $"{info[..plus]} ({info[(plus + 1)..Math.Min(info.Length, plus + 8)]})";
    }
}
