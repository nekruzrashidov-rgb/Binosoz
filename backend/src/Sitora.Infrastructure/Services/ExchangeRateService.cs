using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class ExchangeRateService(
    AppDbContext db,
    IRateProvider provider,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ExchangeRateService> logger) : IExchangeRateService
{
    public async Task<RateResponse> GetCurrentAsync(Currency currency, Guid? organizationId, CancellationToken ct = default)
    {
        EnsureForeign(currency);
        return await FindCurrentAsync(currency, organizationId, ct) ?? throw new NotFoundException("Курс ещё не задан");
    }

    public Task<RateResponse?> FindCurrentAsync(Currency currency, Guid? organizationId, CancellationToken ct = default)
    {
        var today = DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
        return Applicable(currency, today, organizationId)
            .Select(r => new RateResponse(r.Date, r.Currency, r.RateToTjs, r.Source))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RateResponse> SetAsync(SetRateRequest request, CancellationToken ct = default)
    {
        EnsureForeign(request.Currency);
        var today = DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
        // НБТ публикует курс заранее на следующий день, дальше вперёд задавать нельзя.
        if (request.Date > today.AddDays(1))
            throw new BusinessRuleException("Курс можно задать не дальше чем на завтра");

        var rate = await UpsertAsync(currentUser.OrganizationId, request.Currency, request.Date, request.RateToTjs, RateSource.Manual, ct);
        await db.SaveChangesAsync(ct);
        return new RateResponse(rate.Date, rate.Currency, rate.RateToTjs, rate.Source);
    }

    public async Task<decimal?> GetRateOnAsync(Currency currency, DateOnly date, Guid? organizationId, CancellationToken ct = default)
    {
        if (currency == Currency.TJS)
            return 1m;

        return await Applicable(currency, date, organizationId)
            .Select(r => (decimal?)r.RateToTjs)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateFromProviderAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DushanbeTime.Today(now);
        var result = await provider.FetchUsdRateAsync(today, ct);
        // Попытка и её итог сохраняются всегда (C-09): в CRM → «Курс» видно, когда пробовали и почему не вышло.
        await SetSettingAsync(PlatformSettingKeys.RatesLastAttemptAt, Iso(now), ct);
        if (result.Rate is not > 0)
        {
            await SetSettingAsync(PlatformSettingKeys.RatesLastResult, Truncate(result.Error ?? "курс не получен"), ct);
            await db.SaveChangesAsync(ct);
            return;
        }
        await SetSettingAsync(PlatformSettingKeys.RatesLastResult, OkResult, ct);
        await SetSettingAsync(PlatformSettingKeys.RatesLastSuccessAt, Iso(now), ct);

        // Общий курс, введённый вручную, загрузка не перезаписывает.
        if (await db.ExchangeRates.AnyAsync(r => r.OrganizationId == null && r.Currency == Currency.USD
                                                 && r.Date == today && r.Source == RateSource.Manual, ct))
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        await UpsertAsync(null, Currency.USD, today, result.Rate.Value, RateSource.Nbt, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Загружен курс НБТ на {Date}: {Rate}", today, result.Rate);
    }

    private const string OkResult = "ok";

    public async Task<RateUpdateStatus> GetUpdateStatusAsync(CancellationToken ct = default)
    {
        var keys = new[] { PlatformSettingKeys.RatesLastAttemptAt, PlatformSettingKeys.RatesLastResult, PlatformSettingKeys.RatesLastSuccessAt };
        var values = await db.PlatformSettings.AsNoTracking().Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var lastNbt = await db.ExchangeRates.AsNoTracking()
            .Where(r => r.OrganizationId == null && r.Currency == Currency.USD && r.Source == RateSource.Nbt)
            .OrderByDescending(r => r.Date).Select(r => (DateOnly?)r.Date).FirstOrDefaultAsync(ct);
        var result = values.GetValueOrDefault(PlatformSettingKeys.RatesLastResult);
        return new RateUpdateStatus(ParseTime(values.GetValueOrDefault(PlatformSettingKeys.RatesLastAttemptAt)),
            result is null ? null : result == OkResult,
            result is null or OkResult ? null : result,
            ParseTime(values.GetValueOrDefault(PlatformSettingKeys.RatesLastSuccessAt)),
            lastNbt);
    }

    public async Task<bool> NeedsCatchUpAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DushanbeTime.Today(now);
        var latest = await db.ExchangeRates.AsNoTracking()
            .Where(r => r.OrganizationId == null && r.Currency == Currency.USD)
            .OrderByDescending(r => r.Date).Select(r => (DateOnly?)r.Date).FirstOrDefaultAsync(ct);
        if (latest is { } date && date >= today.AddDays(-1))
            return false;
        // Не чаще раза в час: Render будит сервис много раз за день, неудачная попытка не повторяется на каждом старте.
        var attempt = ParseTime(await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == PlatformSettingKeys.RatesLastAttemptAt).Select(s => s.Value).FirstOrDefaultAsync(ct));
        return attempt is null || now - attempt.Value >= TimeSpan.FromHours(1);
    }

    private static string Iso(DateTime utc) => utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTime? ParseTime(string? value) =>
        DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var t)
            ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : null;

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];

    private async Task SetSettingAsync(string key, string value, CancellationToken ct)
    {
        var setting = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
            db.PlatformSettings.Add(setting = new PlatformSetting { Key = key });
        setting.Value = value;
        setting.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    // Последний курс не позже даты: общий или своей организации; на одну дату ручной курс организации важнее общего.
    private IQueryable<ExchangeRate> Applicable(Currency currency, DateOnly date, Guid? organizationId) =>
        db.ExchangeRates.AsNoTracking()
            .Where(r => r.Currency == currency && r.Date <= date
                        && (r.OrganizationId == null || r.OrganizationId == organizationId))
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.OrganizationId != null);

    private async Task<ExchangeRate> UpsertAsync(Guid? organizationId, Currency currency, DateOnly date, decimal value,
        RateSource source, CancellationToken ct)
    {
        var rate = await db.ExchangeRates.FirstOrDefaultAsync(
            r => r.OrganizationId == organizationId && r.Currency == currency && r.Date == date, ct);
        var before = rate is null ? null : new { rate.RateToTjs, rate.Source };
        if (rate is null)
        {
            rate = new ExchangeRate { OrganizationId = organizationId, Currency = currency, Date = date };
            db.ExchangeRates.Add(rate);
        }

        rate.RateToTjs = value;
        rate.Source = source;
        audit.Record(nameof(ExchangeRate), rate.Id, before is null ? "Created" : "Updated", before,
            new { rate.Date, rate.Currency, rate.RateToTjs, rate.Source, rate.OrganizationId });
        return rate;
    }

    private static void EnsureForeign(Currency currency)
    {
        if (currency == Currency.TJS)
            throw new BusinessRuleException("Курс задаётся для иностранной валюты, сомони — базовая валюта");
    }
}
