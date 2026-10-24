using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Payments;

public record CreatePlanRequest(decimal DownPaymentPct, int Months, int PaymentDay, DateOnly StartDate);

// Суммы в валюте договора; *Equivalent — во второй валюте по текущему курсу (null, если курса нет).
public record ScheduleItemResponse(
    Guid Id,
    ScheduleItemKind Kind,
    DateOnly DueDate,
    decimal AmountDue,
    decimal AmountPaid,
    ScheduleItemStatus Status,
    bool IsOverdue,
    decimal? AmountDueEquivalent,
    decimal? AmountPaidEquivalent);

// Оценочный эквивалент графика во второй валюте (TJS ↔ USD) по текущему курсу.
// К оплате принимается сумма по курсу на дату оплаты, поэтому эти цифры только для ориентира.
public record ScheduleEquivalent(
    Currency Currency,
    decimal Rate,
    DateOnly RateDate,
    bool IsEstimate,
    string Note,
    decimal Total,
    decimal Paid,
    decimal Remaining,
    decimal Overdue);

public record ScheduleResponse(
    Guid DealId,
    Currency Currency,
    decimal Total,
    decimal Paid,
    decimal Remaining,
    decimal Overdue,
    decimal DownPaymentPct,
    int Months,
    int PaymentDay,
    DateOnly StartDate,
    IReadOnlyList<ScheduleItemResponse> Items,
    IReadOnlyList<PaymentResponse> Payments,
    ScheduleEquivalent? Equivalent);

public record CreatePaymentRequest(decimal Amount, Currency Currency, DateOnly PaidAt, PaymentMethod Method, string? Comment);

public record ReversePaymentRequest(string Reason);

public record PaymentResponse(
    Guid Id,
    Guid DealId,
    decimal Amount,
    Currency Currency,
    decimal? RateUsed,
    decimal AmountInDealCurrency,
    decimal AmountTjs,
    DateOnly PaidAt,
    PaymentMethod Method,
    string? Comment,
    Guid? ReversalOfId,
    bool IsReversed,
    DateTime CreatedAt);

public record RateResponse(DateOnly Date, Currency Currency, decimal RateToTjs, RateSource Source);

public record SetRateRequest(DateOnly Date, Currency Currency, decimal RateToTjs);

public record StatementPayment(DateOnly PaidAt, decimal Amount, Currency Currency, decimal? RateUsed, decimal AmountInDealCurrency, bool IsReversal);

public record DealHeader(Guid DealId, string BuildingName, int FloorNumber, int ApartmentNumber, string CustomerName);

// Данные PDF графика платежей: шапка сделки и тот же график, что отдаёт GET /deals/{id}/schedule.
public record ScheduleDocumentData(DealHeader Deal, ScheduleResponse Schedule, DateOnly AsOf, DateTime GeneratedAtUtc);

// Данные акта сверки на дату; по ним строится PDF.
public record StatementData(
    DateOnly AsOf,
    DateTime GeneratedAtUtc,
    string BuildingName,
    int FloorNumber,
    int ApartmentNumber,
    string CustomerName,
    Guid DealId,
    Currency Currency,
    decimal ContractTotal,
    decimal Accrued,
    decimal Paid,
    decimal Debt,
    decimal Remaining,
    IReadOnlyList<StatementPayment> Payments);
