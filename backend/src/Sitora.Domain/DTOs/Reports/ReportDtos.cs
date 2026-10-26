using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Reports;

public record PaymentReportRow(
    Guid PaymentId,
    DateOnly PaidAt,
    Guid DealId,
    string CustomerName,
    string BuildingName,
    int ApartmentNumber,
    decimal Amount,
    Currency Currency,
    decimal? RateUsed,
    decimal AmountTjs,
    PaymentMethod Method,
    bool IsReversal,
    string? Comment);

// Поступления за период: платежи и сторно (с минусом), итоги по валюте платежа и общий итог в сомони.
public record PaymentsReportResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<PaymentReportRow> Rows,
    IReadOnlyList<MoneyTotal> TotalsByCurrency,
    decimal TotalTjs);

public record DebtReportRow(
    Guid DealId,
    string CustomerName,
    string Phone,
    string BuildingName,
    int ApartmentNumber,
    Currency Currency,
    decimal Overdue,
    DateOnly OldestOverdueDate,
    int DaysOverdue,
    decimal Remaining);

// Задолженность на дату: сделки, где по графику на эту дату оплачено меньше, чем начислено.
public record DebtsReportResponse(
    DateOnly AsOf,
    IReadOnlyList<DebtReportRow> Rows,
    IReadOnlyList<MoneyTotal> TotalsByCurrency);
