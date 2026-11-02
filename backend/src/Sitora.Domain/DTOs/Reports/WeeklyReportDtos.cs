using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Reports;

public record WeeklyReportResponse(Guid Id, Guid BuildingId, DateOnly PeriodStart, DateOnly PeriodEnd, DateTime GeneratedAt, bool IsManual);

// Сводка за неделю. Суммы кассы — в сомони (операции в долларах пересчитаны по курсу на дату операции).
// Просрочки — на начало понедельника после недели (AsOf): срок раньше AsOf, оплачено не полностью.
public record WeeklySummary(
    string OrganizationName,
    string BuildingName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly AsOf,
    decimal IncomeTjs,
    decimal ExpenseTjs,
    decimal ProfitTjs,
    int NewDeals,
    int OverdueItems,
    int ConstructionPercent);

public record WeeklyPaymentRow(DateOnly PaidAt, string Customer, int Apartment, decimal Amount, Currency Currency, string Method, bool IsReversal);

public record WeeklyCashRow(DateOnly Date, string Category, CashDirection Direction, decimal Amount, Currency Currency, decimal AmountTjs, string Description);

public record WeeklyOverdueRow(string Customer, int Apartment, DateOnly DueDate, decimal Left, Currency Currency, int DaysOverdue);

// Просрочка по сделке целиком — для «топ-5 должников» в PDF.
public record WeeklyDebtorRow(string Customer, int Apartment, decimal Left, Currency Currency, int MaxDaysOverdue);

public record WeeklyReportData(
    WeeklySummary Summary,
    IReadOnlyList<WeeklyPaymentRow> Payments,
    IReadOnlyList<WeeklyCashRow> Cash,
    IReadOnlyList<WeeklyOverdueRow> Overdue,
    IReadOnlyList<WeeklyDebtorRow> TopDebtors,
    DateTime GeneratedAtUtc);
