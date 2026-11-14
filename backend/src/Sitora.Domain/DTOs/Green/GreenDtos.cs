namespace Sitora.Domain.DTOs.Green;

// «Зелёные» настройки застройщика (GET/PUT /owner/green-settings, руководители организации).
public record GreenSettingsResponse(
    decimal? TariffKwhTjs,
    decimal? BaselineKwhPerM2Year,
    string? BaselineSource,
    decimal? Co2KgPerKgPaper,
    string? Co2Source,
    bool IsDemo);

public record SaveGreenSettingsRequest(
    decimal? TariffKwhTjs = null,
    decimal? BaselineKwhPerM2Year = null,
    string? BaselineSource = null,
    decimal? Co2KgPerKgPaper = null,
    string? Co2Source = null,
    bool IsDemo = false);

// GET /apartments/{id}/utility: ориентировочные расходы на энергию по проектным данным дома.
// Если у дома нет расчётного потребления или у застройщика нет тарифа — 204 (расчёт не показывается).
public record UtilityEstimateResponse(
    Guid ApartmentId,
    decimal Area,
    decimal KwhPerM2Year,
    decimal TariffKwhTjs,
    decimal KwhPerYear,
    decimal KwhPerMonth,
    decimal CostPerYearTjs,
    decimal CostPerMonthTjs,
    decimal? BaselineKwhPerM2Year,
    decimal? BaselineKwhPerYear,
    decimal? BaselineCostPerYearTjs,
    decimal? SavingKwhPerYear,
    decimal? SavingCostPerYearTjs,
    string? BaselineSource,
    bool IsDemo);

// «Зелёный эффект» (публичный агрегат GET /public/green-impact и GET /stats/green): только реальные счётчики из БД.
// paperKgSaved = pagesSaved × 0,005 кг (лист A4 80 г/м² весит ≈ 5 г — физика, не оценка).
// co2KgSaved — только если застройщик задал коэффициент с источником; иначе поля нет.
public record GreenImpactResponse(
    int DocumentsDigital,
    int PagesSaved,
    decimal PaperKgSaved,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] decimal? Co2KgSaved,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Co2Source,
    int OnlineActions,
    bool IsDemo);

// Для CRM: то же плюс разбивка действий и страницы по месяцам (последние 12, старые — сначала).
public record GreenStatsResponse(
    GreenImpactResponse Totals,
    int OnlineBookings,
    int OnlinePayments,
    int ChatMessages,
    IReadOnlyList<GreenMonthResponse> Months);

public record GreenMonthResponse(string Month, int Documents, int Pages, decimal PaperKg);
