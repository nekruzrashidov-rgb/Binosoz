using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Напоминания о платежах по графику, в 10:00 по Душанбе:
// - покупателю (кабинет + Telegram + почта, BuyerNotifier): за 3 дня, в день срока, на 1-й день просрочки и ещё раз
//   через 6 дней после первого (7-й день); защита от дублей — по событию, пункту графика и каналу;
// - бухгалтерам дома — в колокольчик и Telegram о просрочке, один раз на пункт графика.
// Работает из фоновых задач без пользователя: фильтры организаций не действуют, видны все дома.
public class PaymentAlertService(AppDbContext db, TelegramAlertService telegram, BuyerNotifier buyers, TimeProvider timeProvider, ILogger<PaymentAlertService> logger)
{
    public const int ReminderDaysBefore = 3;
    // Второе напоминание о просрочке — через столько дней после первого (первое на 1-й день → второе на 7-й).
    public const int SecondOverdueAfterDays = 6;
    // «Напомнить сейчас» из CRM — не чаще раза в сутки на пункт графика.
    public static readonly TimeSpan ManualReminderInterval = TimeSpan.FromDays(1);
    public const string ManualTooSoon = "Напоминание по этому платежу уже отправляли сегодня. Следующее — через сутки после прошлого";
    public const string ItemNotOpen = "Этот платёж уже оплачен или его нет в графике";
    public const string NoCabinet = "У покупателя нет входа в кабинет: напоминание некуда отправить";

    private record OpenItem(Guid DealId, DateOnly DueDate, decimal Left, Currency Currency, int Apartment,
        string Customer, Guid? CustomerUserId, Guid BuildingId, Guid OrganizationId, string? Block, ApartmentKind Kind)
    {
        public UnitRef Unit => new(Apartment, Block, Kind);
    }

    private static PaymentNotice Notice(OpenItem i, int overdueDays) =>
        new(i.DealId, i.DueDate, i.Left, i.Currency, i.Unit, i.CustomerUserId, i.OrganizationId, overdueDays);

    // Пункты со сроком раньше сегодняшнего дня, оплаченные не полностью, по действующим сделкам.
    public async Task<int> NotifyOverdueAsync(CancellationToken ct = default)
    {
        var today = Today;
        var items = await OpenItems(i => i.DueDate < today).ToListAsync(ct);
        var accountants = await AccountantsAsync(items.Select(i => i.OrganizationId).Distinct().ToList(), ct);

        var staff = new List<(Notification, string)>();
        foreach (var i in items)
        {
            var days = today.DayNumber - i.DueDate.DayNumber;
            var money = $"{Money(i.Left)} {i.Currency}";
            foreach (var accountant in accountants.Where(a => a.OrganizationId == i.OrganizationId
                                                               && (a.Buildings.Count == 0 || a.Buildings.Contains(i.BuildingId))))
                staff.Add((New(accountant.Id, Notification.KindOverdue, i, "Просрочка по сделке",
                    $"{i.Customer}, {i.Unit.InText()}: {money} просрочено {days} дн."), TelegramMessages.Overdue(i.Customer, i.Unit, i.Left, i.Currency, days)));
        }
        var count = await SaveNewAsync(staff, "о просрочке", ct);

        // Покупателю: первое напоминание — сразу (1-й день или первый запуск после появления просрочки),
        // второе — через 6 дней после первого.
        var sentFirst = await FirstOverdueSentAsync(items.Select(i => i.DealId).Distinct().ToList(), ct);
        foreach (var i in items.Where(i => i.CustomerUserId is not null))
        {
            var days = today.DayNumber - i.DueDate.DayNumber;
            if (!sentFirst.TryGetValue((i.DealId, i.DueDate, i.CustomerUserId!.Value), out var first))
                count += await buyers.PaymentReminderAsync(Notice(i, days), BuyerEvents.PaymentOverdue, null, ct);
            else if (days >= 7 && DushanbeTime.Today(first).DayNumber <= today.DayNumber - SecondOverdueAfterDays)
                count += await buyers.PaymentReminderAsync(Notice(i, days), BuyerEvents.PaymentOverdueWeek, null, ct);
        }
        return count;
    }

    // Когда покупателю ушло первое напоминание о просрочке: журнал (кабинет) или колокольчик до появления журнала.
    private async Task<Dictionary<(Guid, DateOnly, Guid), DateTime>> FirstOverdueSentAsync(List<Guid> deals, CancellationToken ct)
    {
        var journal = await db.BuyerMessages.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Event == BuyerEvents.PaymentOverdue && m.Channel == BuyerChannels.InApp && m.DealId != null
                        && deals.Contains(m.DealId.Value) && m.DueDate != null)
            .Select(m => new { DealId = m.DealId!.Value, DueDate = m.DueDate!.Value, m.UserId, m.CreatedAt })
            .ToListAsync(ct);
        var bells = await db.Notifications.AsNoTracking()
            .Where(n => n.Kind == Notification.KindOverdue && n.DealId != null && deals.Contains(n.DealId.Value) && n.DueDate != null && n.UserId != null)
            .Select(n => new { DealId = n.DealId!.Value, DueDate = n.DueDate!.Value, UserId = n.UserId!.Value, n.CreatedAt })
            .ToListAsync(ct);
        var result = new Dictionary<(Guid, DateOnly, Guid), DateTime>();
        foreach (var x in journal.Concat(bells))
            if (!result.TryGetValue((x.DealId, x.DueDate, x.UserId), out var at) || x.CreatedAt < at)
                result[(x.DealId, x.DueDate, x.UserId)] = x.CreatedAt;
        return result;
    }

    // Пункты со сроком ровно через 3 дня, оплаченные не полностью.
    public async Task<int> RemindUpcomingAsync(CancellationToken ct = default)
    {
        var due = Today.AddDays(ReminderDaysBefore);
        var items = await OpenItems(i => i.DueDate == due).ToListAsync(ct);
        var count = 0;
        foreach (var i in items.Where(i => i.CustomerUserId is not null))
            count += await buyers.PaymentReminderAsync(Notice(i, 0), BuyerEvents.PaymentReminder, null, ct);
        if (count > 0)
            logger.LogInformation("Напоминаний о платеже через 3 дня: {Count}", count);
        return count;
    }

    // Пункты со сроком сегодня: «Сегодня срок платежа: 12 500 TJS по кв. №…».
    public async Task<int> RemindDueTodayAsync(CancellationToken ct = default)
    {
        var today = Today;
        var items = await OpenItems(i => i.DueDate == today).ToListAsync(ct);
        var count = 0;
        foreach (var i in items.Where(i => i.CustomerUserId is not null))
            count += await buyers.PaymentReminderAsync(Notice(i, 0), BuyerEvents.PaymentDueToday, null, ct);
        if (count > 0)
            logger.LogInformation("Напоминаний «сегодня срок платежа»: {Count}", count);
        return count;
    }

    // «Напомнить сейчас» из CRM: пункт графика должен быть неоплаченным; не чаще раза в сутки на пункт.
    public async Task<int> RemindNowAsync(Guid dealId, DateOnly dueDate, Guid staffUserId, CancellationToken ct = default)
    {
        var item = await OpenItems(i => i.Plan.DealId == dealId && i.DueDate == dueDate).FirstOrDefaultAsync(ct)
                   ?? throw new BusinessRuleException(ItemNotOpen);
        if (item.CustomerUserId is null)
            throw new BusinessRuleException(NoCabinet);
        var since = timeProvider.GetUtcNow().UtcDateTime - ManualReminderInterval;
        if (await db.BuyerMessages.IgnoreQueryFilters().AnyAsync(m => m.DealId == dealId && m.DueDate == dueDate
                && m.Event == BuyerEvents.PaymentManual && m.CreatedAt > since, ct))
            throw new TooManyRequestsException(ManualTooSoon);
        var days = Math.Max(0, Today.DayNumber - dueDate.DayNumber);
        return await buyers.PaymentReminderAsync(Notice(item, days), BuyerEvents.PaymentManual, staffUserId, ct);
    }

    private IQueryable<OpenItem> OpenItems(System.Linq.Expressions.Expression<Func<PaymentScheduleItem, bool>> when) =>
        db.PaymentScheduleItems.AsNoTracking()
            .Where(when)
            .Where(i => i.AmountPaid < i.AmountDue
                        && i.Plan.Deal.Stage != DealStage.Cancelled && i.Plan.Deal.Stage != DealStage.Completed)
            .Select(i => new OpenItem(i.Plan.DealId, i.DueDate, i.AmountDue - i.AmountPaid, i.Plan.Deal.Currency,
                i.Plan.Deal.Apartment.Number, i.Plan.Deal.Customer.FullName, i.Plan.Deal.Customer.UserId,
                i.Plan.Deal.Apartment.Floor.BuildingId, i.Plan.Deal.Apartment.Floor.Building.OrganizationId,
                i.Plan.Deal.Apartment.Floor.Building.BlockCode, i.Plan.Deal.Apartment.Kind));

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
