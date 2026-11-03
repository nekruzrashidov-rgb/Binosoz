using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Онлайн-оплата картой через IPaymentGateway. Попытка — OnlinePayment (Pending); когда шлюз ответит «оплачено»,
// появляется обычный платёж по графику (метод Card) через PaymentService — с его проверками, пересчётом графика,
// журналом и уведомлениями. Платежи по-прежнему не удаляются: возврат — сторно.
public class OnlinePaymentService(
    AppDbContext db,
    IPaymentGateway gateway,
    IPaymentService payments,
    DealAccess access,
    ICurrentUser currentUser,
    IAuditService audit,
    TelegramAlertService telegram,
    TimeProvider timeProvider,
    ILogger<OnlinePaymentService> logger) : IOnlinePaymentService
{
    private const string DealNotFound = "Сделка не найдена";
    private const string DefaultReturnUrl = "/cabinet?success=1";

    public async Task<InitiatePaymentResponse> InitiateAsync(Guid dealId, InitiatePaymentRequest request, CancellationToken ct = default)
    {
        // GetScheduleAsync проверяет, что сделка своя (чужая — 404), и даёт остаток и ближайший платёж.
        var schedule = await payments.GetScheduleAsync(dealId, ct);
        var deal = await db.Deals.AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new { d.Stage, d.Apartment.Number }).SingleAsync(ct);
        if (deal.Stage == DealStage.Cancelled)
            throw new BusinessRuleException("Сделка отменена");
        if (schedule.Remaining <= 0)
            throw new BusinessRuleException("По сделке всё оплачено");

        var next = schedule.Items.Where(i => i.Status != ScheduleItemStatus.Paid).OrderBy(i => i.DueDate).FirstOrDefault();
        var amount = request.Amount ?? (next is null ? schedule.Remaining : next.AmountDue - next.AmountPaid);
        if (amount <= 0)
            throw new BusinessRuleException("Сумма оплаты должна быть больше нуля");
        if (amount > schedule.Remaining)
            throw new BusinessRuleException($"Сумма больше остатка по договору. Остаток: {schedule.Remaining:0.00} {schedule.Currency}");

        var init = await gateway.InitiateAsync(new PaymentInitRequest(dealId, amount, schedule.Currency.ToString(),
            $"Оплата по договору: кв. {deal.Number}", SafeReturnUrl(request.ReturnUrl), $"/api/v1/payment/webhook/{gateway.ProviderName.ToLowerInvariant()}"));
        if (!init.Success || init.GatewayPaymentId is null || init.PaymentUrl is null)
            throw new ServiceUnavailableException(init.ErrorMessage ?? "Платёжный шлюз не принял платёж, попробуйте позже");

        var online = new OnlinePayment
        {
            DealId = dealId,
            Provider = gateway.ProviderName,
            GatewayPaymentId = init.GatewayPaymentId,
            Amount = amount,
            Currency = schedule.Currency,
            CreatedById = currentUser.UserId
        };
        db.OnlinePayments.Add(online);
        audit.Record(nameof(OnlinePayment), online.Id, "Initiated", null,
            new { online.DealId, online.Provider, online.GatewayPaymentId, online.Amount, online.Currency }, await db.OrganizationOfDealAsync(dealId, ct));
        await db.SaveChangesAsync(ct);
        return new InitiatePaymentResponse(init.PaymentUrl, init.GatewayPaymentId, amount, schedule.Currency, gateway.ProviderName);
    }

    // Узнаёт у шлюза результат. «Оплачено» превращается в платёж по графику ровно один раз: строку забирает тот запрос,
    // которому удалось сменить статус Pending → Paid (остальные одновременные запросы увидят уже Paid).
    public async Task<OnlinePaymentStatusResponse> GetStatusAsync(Guid dealId, string gatewayPaymentId, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException(DealNotFound);
        var online = await db.OnlinePayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.DealId == dealId && p.GatewayPaymentId == gatewayPaymentId, ct)
            ?? throw new NotFoundException("Онлайн-оплата не найдена");
        if (online.Status != PaymentGatewayStatus.Pending || online.Provider != gateway.ProviderName)
            return ToResponse(online);

        PaymentStatusResult status;
        try
        {
            status = await gateway.CheckStatusAsync(gatewayPaymentId);
        }
        catch (NotFoundException)
        {
            // Шлюз платёж не помнит (имитация после перезапуска): остаётся то, что записано у нас.
            logger.LogWarning("Шлюз {Provider} не знает платёж {GatewayPaymentId}", online.Provider, gatewayPaymentId);
            return ToResponse(online);
        }

        if (status.Status == PaymentGatewayStatus.Paid)
            await RecordPaidAsync(online, status.PaidAt ?? timeProvider.GetUtcNow().UtcDateTime, ct);
        else if (status.Status != PaymentGatewayStatus.Pending)
            await SetStatusAsync(online, status.Status, ct);

        return ToResponse(await db.OnlinePayments.AsNoTracking().SingleAsync(p => p.Id == online.Id, ct));
    }

    public async Task<OnlinePaymentStatusResponse> SimulateAsync(SimulatePaymentRequest request, CancellationToken ct = default)
    {
        if (gateway is not MockPaymentGateway mock)
            throw new NotFoundException("Имитация оплаты доступна только с тестовым шлюзом");
        var result = await mock.SimulatePaymentAsync(request.GatewayPaymentId, request.Success);
        var online = await db.OnlinePayments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Provider == mock.ProviderName && p.GatewayPaymentId == request.GatewayPaymentId, ct);
        return online is null
            ? new OnlinePaymentStatusResponse(result.GatewayPaymentId, result.Status, result.Amount, Enum.Parse<Currency>(result.Currency), result.PaidAt, null)
            : ToResponse(online) with { Status = result.Status, PaidAt = result.PaidAt };
    }

    private async Task RecordPaidAsync(OnlinePayment online, DateTime paidAt, CancellationToken ct)
    {
        var claimed = await db.OnlinePayments
            .Where(p => p.Id == online.Id && p.Status == PaymentGatewayStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentGatewayStatus.Paid).SetProperty(p => p.PaidAt, paidAt), ct);
        if (claimed == 0)
            return;

        try
        {
            var payment = await payments.AddPaymentAsync(online.DealId, new CreatePaymentRequest(online.Amount, online.Currency,
                DushanbeTime.Today(paidAt), PaymentMethod.Card, $"Онлайн-оплата картой ({online.Provider}, {online.GatewayPaymentId})"), ct);
            await db.OnlinePayments.Where(p => p.Id == online.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentId, payment.Id), ct);
            audit.Record(nameof(OnlinePayment), online.Id, "Paid", new { Status = nameof(PaymentGatewayStatus.Pending) },
                new { Status = nameof(PaymentGatewayStatus.Paid), PaymentId = payment.Id, online.Amount, online.Currency }, await db.OrganizationOfDealAsync(online.DealId, ct));
            await db.SaveChangesAsync(ct);
            await NotifyCustomerAsync(online, ct);
        }
        catch (Exception e) when (e is DomainException)
        {
            // Деньги банк взял, а платёж по графику не записался (например, остаток уже закрыт): разбирает бухгалтер.
            audit.Record(nameof(OnlinePayment), online.Id, "PaidNotRecorded", null, new { Error = e.Message }, await db.OrganizationOfDealAsync(online.DealId, ct));
            await db.SaveChangesAsync(ct);
            logger.LogError(e, "Онлайн-оплата {GatewayPaymentId} оплачена, но платёж по графику не записан", online.GatewayPaymentId);
        }
    }

    private async Task SetStatusAsync(OnlinePayment online, PaymentGatewayStatus status, CancellationToken ct)
    {
        var changed = await db.OnlinePayments.Where(p => p.Id == online.Id && p.Status == PaymentGatewayStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status), ct);
        if (changed == 0)
            return;
        audit.Record(nameof(OnlinePayment), online.Id, status.ToString(), new { Status = nameof(PaymentGatewayStatus.Pending) },
            new { Status = status.ToString() }, await db.OrganizationOfDealAsync(online.DealId, ct));
        await db.SaveChangesAsync(ct);
    }

    private async Task NotifyCustomerAsync(OnlinePayment online, CancellationToken ct)
    {
        var customer = await db.Deals.IgnoreQueryFilters().AsNoTracking().Where(d => d.Id == online.DealId)
            .Select(d => new { d.Customer.UserId, d.Apartment.Number }).SingleAsync(ct);
        if (customer.UserId is { } userId)
            await telegram.SendToUsersAsync([(userId, TelegramMessages.OnlinePayment(customer.Number, online.Amount, online.Currency))], ct);
    }

    // Только путь на этом сайте: иначе ссылку оплаты можно было бы использовать для перехода на чужой сайт.
    private static string SafeReturnUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.Contains('\\')
            ? url.Trim()
            : DefaultReturnUrl;

    private static OnlinePaymentStatusResponse ToResponse(OnlinePayment p) =>
        new(p.GatewayPaymentId, p.Status, p.Amount, p.Currency, p.PaidAt, p.PaymentId);
}
