using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Имитация банка для разработки и демонстрации: платежи живут в памяти процесса (после перезапуска шлюз их не помнит).
// Страница «банка» — /payment/mock-card; результат оплаты задаёт SimulatePaymentAsync (в API — только в Development).
// Singleton: журнал пишется через отдельную область DI, потому что AuditService и база — scoped.
public class MockPaymentGateway(IServiceScopeFactory scopes, TimeProvider timeProvider) : IPaymentGateway
{
    public const string Name = "Mock";
    public const string PageUrl = "/payment/mock-card";

    private sealed record Entry(Guid DealId, decimal Amount, string Currency, PaymentGatewayStatus Status, DateTime? PaidAt);

    private readonly ConcurrentDictionary<string, Entry> _payments = new();

    public string ProviderName => Name;

    public Task<PaymentInitResult> InitiateAsync(PaymentInitRequest request)
    {
        if (request.Amount <= 0)
            return Task.FromResult(new PaymentInitResult(false, null, null, "Сумма оплаты должна быть больше нуля"));
        var id = "mock_" + Guid.NewGuid();
        _payments[id] = new Entry(request.DealId, request.Amount, request.Currency, PaymentGatewayStatus.Pending, null);
        var url = $"{PageUrl}?id={Uri.EscapeDataString(id)}&amount={request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&currency={Uri.EscapeDataString(request.Currency)}&returnUrl={Uri.EscapeDataString(request.ReturnUrl)}" +
                  // Назначение платежа — его показывает страница банка.
                  $"&description={Uri.EscapeDataString(request.Description)}";
        return Task.FromResult(new PaymentInitResult(true, id, url, null));
    }

    public Task<PaymentStatusResult> CheckStatusAsync(string gatewayPaymentId)
    {
        var entry = Find(gatewayPaymentId);
        return Task.FromResult(new PaymentStatusResult(gatewayPaymentId, entry.Status, entry.Amount, entry.Currency, entry.PaidAt));
    }

    // Возврат — только оплаченного платежа и только один раз.
    public async Task<RefundResult> RefundAsync(string gatewayPaymentId, decimal amount)
    {
        if (!_payments.TryGetValue(gatewayPaymentId, out var entry))
            return new RefundResult(false, "Платёж не найден в платёжном шлюзе");
        if (entry.Status == PaymentGatewayStatus.Refunded)
            return new RefundResult(false, "Платёж уже возвращён");
        if (entry.Status != PaymentGatewayStatus.Paid)
            return new RefundResult(false, "Вернуть можно только оплаченный платёж");
        if (amount <= 0 || amount > entry.Amount)
            return new RefundResult(false, $"Сумма возврата — от 0 до {entry.Amount:0.00} {entry.Currency}");
        if (!_payments.TryUpdate(gatewayPaymentId, entry with { Status = PaymentGatewayStatus.Refunded }, entry))
            return new RefundResult(false, "Платёж уже изменился, повторите запрос");

        await AuditAsync(entry.DealId, "Refunded", gatewayPaymentId,
            new { Message = $"Сторно платежа {gatewayPaymentId} на сумму {amount:0.00} {entry.Currency}", Amount = amount, entry.Currency });
        return new RefundResult(true, null);
    }

    // Только у имитации: «банк» принял (Paid) или отклонил (Failed) платёж. Решённый платёж больше не меняется.
    public async Task<PaymentStatusResult> SimulatePaymentAsync(string gatewayPaymentId, bool success)
    {
        var entry = Find(gatewayPaymentId);
        if (entry.Status != PaymentGatewayStatus.Pending)
            throw new BusinessRuleException("Платёж уже обработан банком");
        var updated = success
            ? entry with { Status = PaymentGatewayStatus.Paid, PaidAt = timeProvider.GetUtcNow().UtcDateTime }
            : entry with { Status = PaymentGatewayStatus.Failed };
        if (!_payments.TryUpdate(gatewayPaymentId, updated, entry))
            throw new ConflictException("Платёж уже изменился, повторите запрос");

        await AuditAsync(entry.DealId, success ? "SimulatedPaid" : "SimulatedFailed", gatewayPaymentId,
            new { entry.Amount, entry.Currency, Status = updated.Status.ToString() });
        return new PaymentStatusResult(gatewayPaymentId, updated.Status, updated.Amount, updated.Currency, updated.PaidAt);
    }

    private Entry Find(string gatewayPaymentId) =>
        _payments.TryGetValue(gatewayPaymentId ?? "", out var entry) ? entry : throw new NotFoundException("Платёж не найден в платёжном шлюзе");

    // Запись журнала по сделке: «PaymentGateway» / действие, с номером платежа шлюза.
    private async Task AuditAsync(Guid dealId, string action, string gatewayPaymentId, object after)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var organizationId = await db.OrganizationOfDealAsync(dealId, default);
        audit.Record("PaymentGateway", dealId, action, null, new { Provider = Name, GatewayPaymentId = gatewayPaymentId, Details = after }, organizationId);
        await db.SaveChangesAsync();
    }
}
