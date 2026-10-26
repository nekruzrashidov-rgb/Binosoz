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
        var today = DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
        var rate = await provider.GetUsdRateAsync(today, ct);
        if (rate is not > 0)
            return;

        // Общий курс, введённый вручную, загрузка не перезаписывает.
        if (await db.ExchangeRates.AnyAsync(r => r.OrganizationId == null && r.Currency == Currency.USD
                                                 && r.Date == today && r.Source == RateSource.Manual, ct))
            return;

        await UpsertAsync(null, Currency.USD, today, rate.Value, RateSource.Nbt, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Загружен курс НБТ на {Date}: {Rate}", today, rate);
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
