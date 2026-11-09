using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Инструменты CRM. Данные ограничены организацией и домами сотрудника фильтрами AppDbContext.
public class CrmToolsService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    IExchangeRateService rates,
    TimeProvider time) : ICrmToolsService
{
    public const int MaxImportRows = 1000;

    // ---------- импорт клиентов ----------

    // Повтор телефона (в базе застройщика или выше в файле) — пропуск, а не ошибка: файл можно загружать повторно.
    public async Task<ClientImportResult> ImportClientsAsync(Stream xlsx, bool dryRun, CancellationToken ct = default)
    {
        var organizationId = currentUser.OrganizationId
            ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец");
        // Дом клиентов: у сотрудника с одним домом — его дом; с несколькими домами импорт не угадывает, куда класть клиентов.
        Guid? homeBuildingId = currentUser.BuildingIds switch
        {
            null => null,
            [var single] => single,
            _ => throw new BusinessRuleException("Импорт доступен сотрудникам со всеми домами организации или с одним домом")
        };

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(xlsx);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new BusinessRuleException("Не удалось прочитать файл Excel");
        }

        var rows = new List<ClientImportRow>();
        using (workbook)
        {
            var sheetRows = workbook.Worksheets.First().RowsUsed().Skip(1).ToList();
            if (sheetRows.Count > MaxImportRows)
                throw new BusinessRuleException($"В файле больше {MaxImportRows} строк: разделите его на части");

            // Телефоны застройщика — все, в том числе клиентов чужих домов (телефон уникален в организации).
            var known = (await db.Customers.IgnoreQueryFilters().Where(c => c.OrganizationId == organizationId)
                .Select(c => c.Phone).ToListAsync(ct)).ToHashSet();
            foreach (var row in sheetRows)
            {
                var name = row.Cell(1).GetString().Trim();
                var rawPhone = row.Cell(2).GetString().Trim();
                var rawEmail = row.Cell(3).GetString().Trim();
                var comment = row.Cell(4).GetString().Trim();
                if (name.Length == 0 && rawPhone.Length == 0 && rawEmail.Length == 0)
                    continue;

                var phone = PhoneNumber.Normalize(rawPhone);
                var email = rawEmail.Length == 0 ? null : EmailAddress.Normalize(rawEmail);
                string? error = name.Length == 0 ? "нет имени"
                    : name.Length > 200 ? "имя длиннее 200 символов"
                    : phone is null ? "неверный телефон"
                    : rawEmail.Length > 0 && email is null ? "неверная почта"
                    : null;
                var status = error is not null ? "invalid" : known.Add(phone!) ? "new" : "duplicate";
                rows.Add(new ClientImportRow(row.RowNumber(), name, phone ?? rawPhone, email ?? (rawEmail.Length > 0 ? rawEmail : null),
                    comment.Length > 0 ? comment : null, status, error));
            }
        }

        var added = rows.Where(r => r.Status == "new").ToList();
        if (!dryRun && added.Count > 0)
        {
            foreach (var r in added)
            {
                var customer = new Customer { OrganizationId = organizationId, HomeBuildingId = homeBuildingId, FullName = r.Name, Phone = r.Phone, Email = r.Email };
                db.Customers.Add(customer);
                audit.Record(nameof(Customer), customer.Id, "Imported", null,
                    new { customer.FullName, customer.Phone, customer.Email, HasPassportData = false });
            }
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.IsUniqueViolation())
            {
                throw new ConflictException("Пока шёл импорт, такой телефон завели вручную. Загрузите файл ещё раз");
            }
        }
        return new ClientImportResult(dryRun, added.Count, rows.Count(r => r.Status == "duplicate"), rows.Count(r => r.Status == "invalid"), rows);
    }

    // ---------- шаблоны сообщений ----------

    public async Task<IReadOnlyList<MessageTemplateResponse>> TemplatesAsync(CancellationToken ct = default) =>
        await db.MessageTemplates.AsNoTracking().OrderBy(t => t.Type).ThenBy(t => t.Name)
            .Select(t => new MessageTemplateResponse(t.Id, t.Name, t.Text, t.Type, t.UpdatedAt)).ToListAsync(ct);

    public async Task<MessageTemplateResponse> CreateTemplateAsync(MessageTemplateRequest request, CancellationToken ct = default)
    {
        var organizationId = currentUser.OrganizationId
            ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец");
        var (name, text, type) = Validate(request);
        var template = new MessageTemplate { OrganizationId = organizationId, Name = name, Text = text, Type = type };
        db.MessageTemplates.Add(template);
        audit.Record(nameof(MessageTemplate), template.Id, "Created", null, new { name, type });
        await SaveTemplateAsync(ct);
        return ToResponse(template);
    }

    public async Task<MessageTemplateResponse> UpdateTemplateAsync(Guid id, MessageTemplateRequest request, CancellationToken ct = default)
    {
        var template = await db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Шаблон не найден");
        var (name, text, type) = Validate(request);
        audit.Record(nameof(MessageTemplate), template.Id, "Updated",
            new { template.Name, template.Type, template.Text }, new { Name = name, Type = type, Text = text }, template.OrganizationId);
        template.Name = name;
        template.Text = text;
        template.Type = type;
        await SaveTemplateAsync(ct);
        return ToResponse(template);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Шаблон не найден");
        db.MessageTemplates.Remove(template);
        audit.Record(nameof(MessageTemplate), template.Id, "Deleted", new { template.Name, template.Type }, null, template.OrganizationId);
        await db.SaveChangesAsync(ct);
    }

    private static (string Name, string Text, string Type) Validate(MessageTemplateRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        var text = request.Text?.Trim() ?? "";
        var type = request.Type?.Trim().ToLowerInvariant() ?? "";
        if (name.Length is 0 or > 120)
            throw new BusinessRuleException("Название шаблона — от 1 до 120 символов");
        if (text.Length is 0 or > 1000)
            throw new BusinessRuleException("Текст шаблона — от 1 до 1000 символов");
        if (!MessageTemplateTypes.All.Contains(type))
            throw new BusinessRuleException("Тип шаблона — платёж, бронь или приветствие");
        return (name, text, type);
    }

    private async Task SaveTemplateAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException("Шаблон с таким названием уже есть");
        }
    }

    private static MessageTemplateResponse ToResponse(MessageTemplate t) => new(t.Id, t.Name, t.Text, t.Type, t.UpdatedAt);

    // ---------- аналитика ----------

    // Сделки по месяцам (по Душанбе): заключённые — по дате создания, кроме отменённых; завершённые — по дате последнего
    // изменения сделки на этапе «Завершена». Воронка — за всё время: заявки, брони, сделки (кроме отменённых), завершённые.
    public async Task<SalesAnalyticsResponse> SalesAnalyticsAsync(CancellationToken ct = default)
    {
        var today = DushanbeTime.Today(time.GetUtcNow().UtcDateTime);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var since = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DushanbeTime.Offset;

        var created = await db.Deals.AsNoTracking().Where(d => d.CreatedAt >= since && d.Stage != DealStage.Cancelled)
            .Select(d => d.CreatedAt).ToListAsync(ct);
        var completed = await db.Deals.AsNoTracking().Where(d => d.Stage == DealStage.Completed && d.UpdatedAt >= since)
            .Select(d => d.UpdatedAt).ToListAsync(ct);
        static string Key(DateTime utc) => DushanbeTime.ToLocal(utc).ToString("yyyy-MM");
        var createdBy = created.GroupBy(Key).ToDictionary(g => g.Key, g => g.Count());
        var completedBy = completed.GroupBy(Key).ToDictionary(g => g.Key, g => g.Count());
        var months = Enumerable.Range(0, 12).Select(i => first.AddMonths(i).ToString("yyyy-MM"))
            .Select(m => new SalesMonthRow(m, createdBy.GetValueOrDefault(m), completedBy.GetValueOrDefault(m)))
            .ToList();

        var counts = new (string Key, int Count)[]
        {
            ("leads", await db.Leads.CountAsync(ct)),
            ("bookings", await db.Bookings.CountAsync(ct)),
            ("deals", await db.Deals.CountAsync(d => d.Stage != DealStage.Cancelled, ct)),
            ("completed", await db.Deals.CountAsync(d => d.Stage == DealStage.Completed, ct))
        };
        // Сделку можно завести и без заявки, поэтому база процентов — самый большой шаг, а не обязательно заявки.
        var basis = Math.Max(1, counts.Max(c => c.Count));
        var funnel = counts.Select(c => new FunnelStep(c.Key, c.Count, Math.Round(c.Count * 100m / basis, 1))).ToList();
        return new SalesAnalyticsResponse(months, funnel);
    }

    // Карточка каждого видимого дома: готовность стройки (среднее по этапам), следующий незавершённый этап,
    // квартиры по статусам, выручка (поступления за вычетом сторно, в сомони) и план (стоимость всех квартир по прайсу).
    public async Task<IReadOnlyList<ProjectCardResponse>> ProjectCardsAsync(CancellationToken ct = default)
    {
        var buildings = await db.Buildings.AsNoTracking().OrderBy(b => b.NameRu)
            .Select(b => new { b.Id, b.NameRu, b.NameTg, b.CompletionYear, Developer = b.Organization.Name })
            .ToListAsync(ct);
        if (buildings.Count == 0)
            return [];

        var stages = await db.ConstructionStages.AsNoTracking().OrderBy(s => s.SortOrder)
            .Select(s => new { s.BuildingId, s.Name, s.Percent }).ToListAsync(ct);
        var apartments = await db.Apartments.AsNoTracking()
            .GroupBy(a => new { a.Floor.BuildingId, a.Status, a.Currency })
            .Select(g => new { g.Key.BuildingId, g.Key.Status, g.Key.Currency, Count = g.Count(), Price = g.Sum(a => a.Price) })
            .ToListAsync(ct);
        var revenue = await db.Payments.AsNoTracking().Where(p => p.Deal.Stage != DealStage.Cancelled)
            .GroupBy(p => p.Deal.Apartment.Floor.BuildingId)
            .Select(g => new { BuildingId = g.Key, Tjs = g.Sum(p => p.AmountTjs) })
            .ToDictionaryAsync(x => x.BuildingId, x => x.Tjs, ct);
        var usdRate = (await rates.FindCurrentAsync(Currency.USD, currentUser.OrganizationId, ct))?.RateToTjs ?? 0m;
        var tajik = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";

        return buildings.Select(b =>
        {
            var own = stages.Where(s => s.BuildingId == b.Id).ToList();
            var apts = apartments.Where(a => a.BuildingId == b.Id).ToList();
            int Count(ApartmentStatus status) => apts.Where(a => a.Status == status).Sum(a => a.Count);
            var plan = apts.Sum(a => a.Currency == Currency.USD ? a.Price * usdRate : a.Price);
            return new ProjectCardResponse(
                b.Id,
                tajik && !string.IsNullOrWhiteSpace(b.NameTg) ? b.NameTg : b.NameRu,
                b.Developer,
                own.Count == 0 ? 0 : (int)Math.Round(own.Average(s => s.Percent)),
                own.FirstOrDefault(s => s.Percent < 100)?.Name,
                b.CompletionYear,
                apts.Sum(a => a.Count),
                Count(ApartmentStatus.Sold),
                Count(ApartmentStatus.Booked),
                Count(ApartmentStatus.Free) + Count(ApartmentStatus.OnRequest),
                Math.Round(revenue.GetValueOrDefault(b.Id), 2),
                Math.Round(plan, 2),
                nameof(Currency.TJS));
        }).ToList();
    }
}
