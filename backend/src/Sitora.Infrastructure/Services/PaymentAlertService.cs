using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Уведомления в приложении о платежах по графику: просрочка (покупателю и бухгалтерам дома)
// и напоминание за 3 дня до срока (покупателю). Каждое уведомление — один раз на пункт графика и получателя;
// новое уведомление дублируется в Telegram тем, у кого привязан чат.
// Работает из фоновых задач без пользователя: фильтры организаций не действуют, видны все дома.
public class PaymentAlertService(AppDbContext db, TelegramAlertService telegram, TimeProvider timeProvider, ILogger<PaymentAlertService> logger)
{
    public const int ReminderDaysBefore = 3;

    private record OpenItem(Guid DealId, DateOnly DueDate, decimal Left, Currency Currency, int Apartment,
        string Customer, Guid? CustomerUserId, Guid BuildingId, Guid OrganizationId);

    // Пункты со сроком раньше сегодняшнего дня, оплаченные не полностью, по действующим сделкам.
    public async Task<int> NotifyOverdueAsync(CancellationToken ct = default)
    {
        var today = Today;
        var items = await OpenItems(i => i.DueDate < today).ToListAsync(ct);
        var accountants = await AccountantsAsync(items.Select(i => i.OrganizationId).Distinct().ToList(), ct);

        var notifications = new List<(Notification, string)>();
        foreach (var i in items)
        {
            var days = today.DayNumber - i.DueDate.DayNumber;
            var money = $"{Money(i.Left)} {i.Currency}";
            var telegramText = TelegramMessages.Overdue(i.Customer, i.Apartment, i.Left, i.Currency, days);
            if (i.CustomerUserId is { } customer)
                notifications.Add((New(customer, Notification.KindOverdue, i, "Просроченный платёж",
                    $"Платёж {money} по кв. №{i.Apartment} просрочен на {days} дн."), telegramText));
            foreach (var accountant in accountants.Where(a => a.OrganizationId == i.OrganizationId
                                                               && (a.Buildings.Count == 0 || a.Buildings.Contains(i.BuildingId))))
                notifications.Add((New(accountant.Id, Notification.KindOverdue, i, "Просрочка по сделке",
                    $"{i.Customer}, кв. №{i.Apartment}: {money} просрочено {days} дн."), telegramText));
        }
        return await SaveNewAsync(notifications, "о просрочке", ct);
    }

    // Пункты со сроком ровно через 3 дня, оплаченные не полностью.
    public async Task<int> RemindUpcomingAsync(CancellationToken ct = default)
    {
        var due = Today.AddDays(ReminderDaysBefore);
        var items = await OpenItems(i => i.DueDate == due).ToListAsync(ct);
        var notifications = items.Where(i => i.CustomerUserId is not null)
            .Select(i => (New(i.CustomerUserId!.Value, Notification.KindReminder, i, "Напоминание о платеже",
                $"Через {ReminderDaysBefore} дня ({i.DueDate:dd.MM.yyyy}) платёж {Money(i.Left)} {i.Currency} по кв. №{i.Apartment}"),
                TelegramMessages.Reminder(i.Left, i.Currency, i.DueDate)))
            .ToList();
        return await SaveNewAsync(notifications, "о платеже через 3 дня", ct);
    }

    private IQueryable<OpenItem> OpenItems(System.Linq.Expressions.Expression<Func<PaymentScheduleItem, bool>> when) =>
        db.PaymentScheduleItems.AsNoTracking()
            .Where(when)
            .Where(i => i.AmountPaid < i.AmountDue
                        && i.Plan.Deal.Stage != DealStage.Cancelled && i.Plan.Deal.Stage != DealStage.Completed)
            .Select(i => new OpenItem(i.Plan.DealId, i.DueDate, i.AmountDue - i.AmountPaid, i.Plan.Deal.Currency,
                i.Plan.Deal.Apartment.Number, i.Plan.Deal.Customer.FullName, i.Plan.Deal.Customer.UserId,
                i.Plan.Deal.Apartment.Floor.BuildingId, i.Plan.Deal.Apartment.Floor.Building.OrganizationId));

    private record Accountant(Guid Id, Guid OrganizationId, List<Guid> Buildings);

    // Активные бухгалтеры организаций; без закреплённых домов бухгалтер ведёт все дома организации.
    private async Task<List<Accountant>> AccountantsAsync(List<Guid> organizations, CancellationToken ct)
    {
        if (organizations.Count == 0)
            return [];
        var roleId = await db.Roles.Where(r => r.Name == RoleNames.Accountant).Select(r => r.Id).SingleAsync(ct);
        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.OrganizationId != null && organizations.Contains(u.OrganizationId.Value)
                        && db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId))
            .Select(u => new Accountant(u.Id, u.OrganizationId!.Value, db.UserBuildings.Where(ub => ub.UserId == u.Id).Select(ub => ub.BuildingId).ToList()))
            .ToListAsync(ct);
    }

    private Notification New(Guid userId, string kind, OpenItem item, string title, string text) => new()
    {
        UserId = userId,
        Channel = Notification.InAppChannel,
        Kind = kind,
        DealId = item.DealId,
        DueDate = item.DueDate,
        Title = title,
        Text = text,
        SentAt = timeProvider.GetUtcNow().UtcDateTime
    };

    // Сохраняет только то, чего ещё не было (тот же получатель, вид, сделка и срок); повторный запуск ничего не добавляет.
    private async Task<int> SaveNewAsync(List<(Notification Notification, string Telegram)> candidates, string what, CancellationToken ct)
    {
        if (candidates.Count == 0)
            return 0;
        var deals = candidates.Select(c => c.Notification.DealId).Distinct().ToList();
        var kinds = candidates.Select(c => c.Notification.Kind).Distinct().ToList();
        var existing = (await db.Notifications.AsNoTracking()
                .Where(n => n.Kind != null && kinds.Contains(n.Kind) && deals.Contains(n.DealId))
                .Select(n => new { n.UserId, n.Kind, n.DealId, n.DueDate })
                .ToListAsync(ct))
            .Select(n => (n.UserId, n.Kind, n.DealId, n.DueDate)).ToHashSet();

        var fresh = candidates.Where(c => existing.Add((c.Notification.UserId, c.Notification.Kind, c.Notification.DealId, c.Notification.DueDate))).ToList();
        if (fresh.Count == 0)
            return 0;
        db.Notifications.AddRange(fresh.Select(c => c.Notification));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Создано уведомлений {What}: {Count}", what, fresh.Count);
        await telegram.SendToUsersAsync(fresh.Select(c => (c.Notification.UserId!.Value, c.Telegram)).ToList(), ct);
        return fresh.Count;
    }

    private static string Money(decimal amount) => amount.ToString("#,0.00", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));

    private DateOnly Today => DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
}
