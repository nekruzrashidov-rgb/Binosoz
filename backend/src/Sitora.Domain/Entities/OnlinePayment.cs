using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.Domain.Entities;

// Попытка онлайн-оплаты картой через платёжный шлюз. Это ещё не платёж: в Payments (деньги, которые пришли)
// запись появляется, только когда шлюз подтвердит оплату, — с методом Card и ссылкой сюда (PaymentId).
public class OnlinePayment : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string GatewayPaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public PaymentGatewayStatus Status { get; set; } = PaymentGatewayStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public Guid? CreatedById { get; set; }
}
