using System.Text.Json.Serialization;

namespace Sitora.Domain.DTOs.Settings;

// Условия рассрочки застройщика: настройки CRM (владелец) и они же в ответе дома для сайта.
public record InstallmentSettingsResponse(decimal? DownPaymentMinPct, int? MaxMonths, bool NoOverpayment);

public record SaveInstallmentSettingsRequest(decimal? DownPaymentMinPct = null, int? MaxMonths = null, bool NoOverpayment = false);

// В доме каталога: только заданные условия (null в JSON не попадает); весь объект — только если что-то задано.
public record InstallmentTermsResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? DownPaymentMinPct,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxMonths,
    bool NoOverpayment);
