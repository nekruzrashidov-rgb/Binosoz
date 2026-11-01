using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Cash;

// Сумма всегда положительная: знак ставит направление.
public record CreateCashTransactionRequest(
    decimal Amount,
    Currency Currency,
    CashDirection Direction,
    CashCategory Category,
    string Description,
    DateOnly OperationDate,
    Guid? DealId);

public record ReverseCashTransactionRequest(string Reason);

public record CashTransactionResponse(
    Guid Id,
    Guid BuildingId,
    decimal Amount,
    Currency Currency,
    decimal AmountTjs,
    CashDirection Direction,
    CashCategory Category,
    string Description,
    Guid? DealId,
    bool IsReversal,
    Guid? ReversalOfId,
    bool IsReversed,
    DateOnly OperationDate,
    DateTime CreatedAt,
    string CreatedByName);

// Итоги в сомони: суммы в USD пересчитаны по курсу на дату операции; сторно уменьшает свою сторону.
public record CashBookResponse(
    Guid BuildingId,
    string BuildingName,
    DateOnly From,
    DateOnly To,
    decimal TotalIncomeTjs,
    decimal TotalExpenseTjs,
    decimal BalanceTjs,
    IReadOnlyList<CashTransactionResponse> Transactions);

// Данные кассовой квитанции для PDF.
public record CashReceiptData(
    string OrganizationName,
    string BuildingName,
    string Number,
    CashTransactionResponse Transaction,
    string CategoryText,
    string? DealText,
    string? ReversalOfNumber,
    DateTime GeneratedAtUtc);
