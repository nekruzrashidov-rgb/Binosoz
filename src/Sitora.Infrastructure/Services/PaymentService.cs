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

public class PaymentService(
    AppDbContext db,
    DealAccess access,
    IExchangeRateService rates,
    StatementService statements,
    ICurrentUser currentUser,
    IAuditService audit,
    INotificationSender notifications,
    TimeProvider timeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const string DealNotFound = "Сделка не найдена";

    public async Task<ScheduleResponse> CreatePlanAsync(Guid dealId, CreatePlanRequest request, CancellationToken ct = default)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var deal = await db.LockDealAsync(dealId, ct);
            if (deal.Stage == DealStage.Cancelled)
                throw new BusinessRuleException("Сделка отменена");
            if (await db.Payments.AnyAsync(p => p.DealId == dealId, ct))
                throw new BusinessRuleException("По сделке уже есть платежи, график менять нельзя");

            var items = PaymentScheduleCalculator.Build(deal.FinalPrice, request.DownPaymentPct, request.Months,
                request.PaymentDay, request.StartDate);

            var existing = await db.PaymentPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.DealId == dealId, ct);
            if (existing is not null)
            {
                audit.Record(nameof(PaymentPlan), existing.Id, "Replaced", PlanSnapshot(existing), null);
                db.PaymentPlans.Remove(existing);
                await db.SaveChangesAsync(ct);
            }

            var plan = new PaymentPlan
            {
                DealId = dealId,
                DownPaymentPct = request.DownPaymentPct,
                Months = request.Months,
                PaymentDay = request.PaymentDay,
                StartDate = request.StartDate,
                Items = items
            };
            db.PaymentPlans.Add(plan);
            audit.Record(nameof(PaymentPlan), plan.Id, "Created", null, PlanSnapshot(plan));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return await GetScheduleAsync(dealId, ct);
    }

    public async Task<ScheduleResponse> GetScheduleAsync(Guid dealId, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException(DealNotFound);

        var currency = await db.Deals.Where(d => d.Id == dealId).Select(d => d.Currency).SingleAsync(ct);
        var plan = await db.PaymentPlans.AsNoTracking().Include(p => p.Items)
                       .FirstOrDefaultAsync(p => p.DealId == dealId, ct)
                   ?? throw new NotFoundException("График платежей ещё не составлен");
        var payments = await LoadPaymentsAsync(dealId, ct);

        var today = Today;
        var usdRate = await rates.FindCurrentAsync(Currency.USD, ct);
        var toEquivalent = EquivalentConverter(currency, usdRate);

        var paid = payments.Sum(p => p.AmountInDealCurrency);
        var items = plan.Items.OrderBy(i => i.DueDate).ThenBy(i => i.Kind)
            .Select(i => new ScheduleItemResponse(i.Id, i.Kind, i.DueDate, i.AmountDue, i.AmountPaid, i.Status,
                i.DueDate < today && i.Status != ScheduleItemStatus.Paid,
                toEquivalent?.Invoke(i.AmountDue), toEquivalent?.Invoke(i.AmountPaid)))
            .ToList();
        var overdue = items.Where(i => i.IsOverdue).Sum(i => i.AmountDue - i.AmountPaid);
        var remaining = plan.Total - paid;

        ScheduleEquivalent? equivalent = null;
        if (toEquivalent is not null && usdRate is not null)
        {
            equivalent = new ScheduleEquivalent(
                currency == Currency.USD ? Currency.TJS : Currency.USD,
                usdRate.RateToTjs,
                usdRate.Date,
                IsEstimate: true,
                $"Оценочно: по курсу {usdRate.RateToTjs:0.0000} TJS за 1 USD на {usdRate.Date:dd.MM.yyyy}. " +
                "Платёж пересчитывается по курсу на дату оплаты.",
                toEquivalent(plan.Total), toEquivalent(paid), toEquivalent(remaining), toEquivalent(overdue));
        }

        return new ScheduleResponse(dealId, currency, plan.Total, paid, remaining, overdue,
            plan.DownPaymentPct, plan.Months, plan.PaymentDay, plan.StartDate, items, payments, equivalent);
    }

    public async Task<byte[]> GetSchedulePdfAsync(Guid dealId, CancellationToken ct = default)
    {
        // GetScheduleAsync сам проверяет, что сделка своя.
        var schedule = await GetScheduleAsync(dealId, ct);
        return await statements.RenderScheduleAsync(dealId, schedule, Today, ct);
    }

    // Перевод суммы из валюты договора во вторую валюту; null, если курса USD ещё нет.
    private static Func<decimal, decimal>? EquivalentConverter(Currency dealCurrency, RateResponse? usdRate)
    {
        if (usdRate is null)
            return null;
        var rate = usdRate.RateToTjs;
        return dealCurrency == Currency.USD
            ? amount => Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero)
            : amount => Math.Round(amount / rate, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<PaymentResponse> AddPaymentAsync(Guid dealId, CreatePaymentRequest request, CancellationToken ct = default)
    {
        Payment payment;
        Guid? customerUserId;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var deal = await db.LockDealAsync(dealId, ct);
            if (deal.Stage == DealStage.Cancelled)
                throw new BusinessRuleException("Сделка отменена");
            if (request.PaidAt > Today)
                throw new BusinessRuleException("Дата оплаты не может быть в будущем");

            var plan = await LoadPlanAsync(dealId, ct);
            var (rateUsed, inDealCurrency, inTjs) = await ConvertAsync(deal.Currency, request.Currency, request.Amount, request.PaidAt, ct);

            var paidBefore = await PaidTotalAsync(dealId, ct);
            if (paidBefore + inDealCurrency > plan.Total)
                throw new BusinessRuleException(
                    $"Сумма больше остатка по договору. Остаток: {plan.Total - paidBefore:0.00} {deal.Currency}");

            payment = new Payment
            {
                DealId = dealId,
                Amount = request.Amount,
                Currency = request.Currency,
                RateUsed = rateUsed,
                AmountInDealCurrency = inDealCurrency,
                AmountTjs = inTjs,
                PaidAt = request.PaidAt,
                Method = request.Method,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                CreatedById = currentUser.UserId
            };
            db.Payments.Add(payment);
            plan.ApplyPayments(paidBefore + inDealCurrency);
            audit.Record(nameof(Payment), payment.Id, "Created", null, PaymentSnapshot(payment));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            customerUserId = await CustomerUserIdAsync(dealId, ct);
        }

        await NotifyCustomerAsync(customerUserId,
            $"Получен платёж {payment.Amount:0.00} {payment.Currency} от {payment.PaidAt:dd.MM.yyyy}", ct);
        return ToResponse(payment, false);
    }

    public async Task<PaymentResponse> ReverseAsync(Guid paymentId, string reason, CancellationToken ct = default)
    {
        var dealId = await db.Payments.Where(p => p.Id == paymentId).Select(p => (Guid?)p.DealId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Платёж не найден");

        Payment reversal;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.LockDealAsync(dealId, ct);
            var original = await db.Payments.SingleAsync(p => p.Id == paymentId, ct);
            if (original.IsReversal)
                throw new BusinessRuleException("Сторно нельзя сторнировать");
            if (await db.Payments.AnyAsync(p => p.ReversalOfId == paymentId, ct))
                throw new ConflictException("Платёж уже сторнирован");

            var plan = await LoadPlanAsync(dealId, ct);
            var paidBefore = await PaidTotalAsync(dealId, ct);

            // Сторно повторяет исходный платёж с обратным знаком и тем же курсом, датой — день исправления.
            reversal = new Payment
            {
                DealId = dealId,
                Amount = -original.Amount,
                Currency = original.Currency,
                RateUsed = original.RateUsed,
                AmountInDealCurrency = -original.AmountInDealCurrency,
                AmountTjs = -original.AmountTjs,
                PaidAt = Today,
                Method = original.Method,
                Comment = reason.Trim(),
                ReversalOfId = original.Id,
                CreatedById = currentUser.UserId
            };
            db.Payments.Add(reversal);
            plan.ApplyPayments(paidBefore + reversal.AmountInDealCurrency);
            audit.Record(nameof(Payment), original.Id, "Reversed", PaymentSnapshot(original), PaymentSnapshot(reversal));

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.IsUniqueViolation())
            {
                throw new ConflictException("Платёж уже сторнирован");
            }

            await transaction.CommitAsync(ct);
        }

        return ToResponse(reversal, false);
    }

    public async Task<StatementData> GetStatementDataAsync(Guid dealId, DateOnly? asOf, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException(DealNotFound);
        return await statements.BuildAsync(dealId, asOf ?? Today, ct);
    }

    public async Task<byte[]> GetStatementPdfAsync(Guid dealId, DateOnly? asOf, CancellationToken ct = default)
    {
        var data = await GetStatementDataAsync(dealId, asOf, ct);
        return statements.Render(data);
    }

    // Возвращает курс платежа, сумму в валюте договора и в сомони.
    private async Task<(decimal? Rate, decimal InDealCurrency, decimal InTjs)> ConvertAsync(
        Currency dealCurrency, Currency paymentCurrency, decimal amount, DateOnly paidAt, CancellationToken ct)
    {
        if (dealCurrency == Currency.TJS && paymentCurrency == Currency.TJS)
            return (null, amount, amount);

        var rate = await rates.GetRateOnAsync(Currency.USD, paidAt, ct)
            ?? throw new BusinessRuleException($"Нет курса USD на {paidAt:dd.MM.yyyy}. Сначала введите курс");

        return (dealCurrency, paymentCurrency) switch
        {
            (Currency.USD, Currency.USD) => (rate, amount, Math.Round(amount * rate, 2)),
            (Currency.USD, Currency.TJS) => (rate, Math.Round(amount / rate, 2), amount),
            _ => (rate, Math.Round(amount * rate, 2), Math.Round(amount * rate, 2))
        };
    }

    private async Task<PaymentPlan> LoadPlanAsync(Guid dealId, CancellationToken ct) =>
        await db.PaymentPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.DealId == dealId, ct)
        ?? throw new BusinessRuleException("Сначала составьте график платежей");

    private Task<decimal> PaidTotalAsync(Guid dealId, CancellationToken ct) =>
        db.Payments.Where(p => p.DealId == dealId).SumAsync(p => p.AmountInDealCurrency, ct);

    private async Task<List<PaymentResponse>> LoadPaymentsAsync(Guid dealId, CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.DealId == dealId)
            .OrderBy(p => p.PaidAt).ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);
        var reversed = payments.Where(p => p.ReversalOfId is not null).Select(p => p.ReversalOfId!.Value).ToHashSet();
        return payments.Select(p => ToResponse(p, reversed.Contains(p.Id))).ToList();
    }

    private Task<Guid?> CustomerUserIdAsync(Guid dealId, CancellationToken ct) =>
        db.Deals.Where(d => d.Id == dealId).Select(d => d.Customer.UserId).SingleAsync(ct);

    private async Task NotifyCustomerAsync(Guid? userId, string text, CancellationToken ct)
    {
        if (userId is null)
            return;
        try
        {
            await notifications.SendAsync(userId, text, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось уведомить покупателя о платеже");
        }
    }

    private DateOnly Today => DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);

    private static object PlanSnapshot(PaymentPlan p) =>
        new { p.DownPaymentPct, p.Months, p.PaymentDay, p.StartDate, p.Total, Items = p.Items.Count };

    private static object PaymentSnapshot(Payment p) =>
        new { p.Amount, p.Currency, p.RateUsed, p.AmountInDealCurrency, p.AmountTjs, p.PaidAt, p.Method, p.ReversalOfId, p.Comment };

    private static PaymentResponse ToResponse(Payment p, bool isReversed) =>
        new(p.Id, p.DealId, p.Amount, p.Currency, p.RateUsed, p.AmountInDealCurrency, p.AmountTjs, p.PaidAt,
            p.Method, p.Comment, p.ReversalOfId, isReversed, p.CreatedAt);
}
