using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

// Кассовая книга дома: каждая операция записывается один раз и не удаляется.
// Ошибку исправляет сторно — новая запись с обратной суммой и ReversalOfId.
public class CashTransaction : BaseEntity
{
    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    // Приход — положительная сумма, расход — отрицательная; у сторно знак обратный исходной операции.
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }

    // Курс USD к сомони на дату операции (как у платежей); null для сомони.
    public decimal? RateUsed { get; set; }
    public decimal AmountTjs { get; set; }

    // У сторно направление и статья — как у исходной операции: так итоги по приходу и расходу сходятся.
    public CashDirection Direction { get; set; }
    public CashCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;

    // Операция по сделке (например, оплата покупателя).
    public Guid? DealId { get; set; }
    public Deal? Deal { get; set; }

    public Guid? ReversalOfId { get; set; }
    public CashTransaction? ReversalOf { get; set; }

    // Календарная дата операции по Душанбе.
    public DateOnly OperationDate { get; set; }
    public Guid CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;

    public bool IsReversal => ReversalOfId is not null;
}
