using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

// Платежи не удаляются и не редактируются. Ошибку исправляет сторно —
// запись с обратными суммами и ссылкой на исходный платёж.
public class Payment : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    // Сумма и валюта, в которой фактически заплатили; у сторно сумма отрицательная.
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }

    // Курс USD к сомони на дату оплаты; null, если пересчёт не нужен (всё в сомони).
    public decimal? RateUsed { get; set; }
    public decimal AmountInDealCurrency { get; set; }
    public decimal AmountTjs { get; set; }

    // Календарная дата оплаты по Душанбе.
    public DateOnly PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Comment { get; set; }

    public Guid? ReversalOfId { get; set; }
    public Payment? ReversalOf { get; set; }

    public Guid? CreatedById { get; set; }

    public bool IsReversal => ReversalOfId is not null;
}
