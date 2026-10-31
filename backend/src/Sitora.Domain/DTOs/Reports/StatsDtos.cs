using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.DTOs.Reports;

// Деньги по валютам и их сумма в сомони. Tjs — null, если есть суммы в USD, а курс ещё не вводили.
public record MoneySummary(IReadOnlyList<MoneyTotal> ByCurrency, decimal? Tjs);

// Сводка для CRM по домам, которые видит сотрудник.
// TotalRevenue — стоимость договоров (этапы «Договор», «Оплата», «Завершена»); в сомони — по текущему курсу.
// CollectedRevenue — поступления по этим договорам за вычетом сторно; в сомони — по курсу на дату каждого платежа.
// Remaining — сколько по ним ещё должны заплатить (в валюте договора).
public record DashboardStatsResponse(
    int TotalApartments,
    int Free,
    int Booked,
    int Sold,
    int OnRequest,
    MoneySummary TotalRevenue,
    MoneySummary CollectedRevenue,
    MoneySummary Remaining,
    int OverduePayments,
    int OverdueDeals,
    int NewLeadsThisWeek,
    int LeadsThisMonth,
    int NewDealsThisMonth,
    decimal ConversionRate,
    decimal? UsdRate);

// Месяц «ГГГГ-ММ»: сделки, заключённые в этом месяце (кроме отменённых), и поступления за месяц за вычетом сторно.
public record MonthlySalesRow(string Month, int Deals, MoneySummary Revenue);

// Дом: квартиры по статусам, занятость (бронь + продано) / всего, стоимость договоров и поступления.
public record BuildingStatsRow(
    Guid BuildingId,
    string Name,
    int TotalApartments,
    int Free,
    int Booked,
    int Sold,
    int OnRequest,
    decimal OccupancyRate,
    MoneySummary Revenue,
    MoneySummary Collected);
