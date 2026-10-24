using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Payments;

public record CreatePlanRequest(decimal DownPaymentPct, int Months, int PaymentDay, DateOnly StartDate);

public record ScheduleItemResponse(
    Guid Id,
    ScheduleItemKind Kind,
    DateOnly DueDate,
    decimal AmountDue,
    decimal AmountPaid,
    ScheduleItemStatus Status,
    bool IsOverdue);

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
    IReadOnlyList<PaymentResponse> Payments);

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
