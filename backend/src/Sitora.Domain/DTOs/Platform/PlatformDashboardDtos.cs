namespace Sitora.Domain.DTOs.Platform;

// Дашборд платформы. Суммы — в сомони (USD по последнему курсу НБТ). «За месяц» — с 1-го числа текущего месяца по Душанбе.
// Сделки — без отменённых. PlatformRevenueMonth — оплаченные тарифы, действующие в этом месяце.
public record PlatformDashboardResponse(
    int TotalOrganizations,
    int ActiveOrganizations,
    int TotalBuildings,
    int TotalDeals,
    decimal TotalPaymentsSum,
    int TotalCustomers,
    int TotalStaff,
    decimal PlatformRevenueMonth,
    int NewOrganizationsMonth,
    int NewDealsMonth,
    IReadOnlyList<TopOrganizationRow> TopOrganizations,
    IReadOnlyList<DealsByMonthRow> DealsByMonth);

public record TopOrganizationRow(Guid Id, string Name, int DealsCount, decimal PaymentsSum);

// Month — «2026-11»; Sum — стоимость новых сделок месяца со скидкой, в сомони.
public record DealsByMonthRow(string Month, int Count, decimal Sum);
