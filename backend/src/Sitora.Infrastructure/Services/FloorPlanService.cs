using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Планы этажей. Какие дома видны, решают фильтры организации и домов в AppDbContext: чужой дом — 404.
// Шаблон вставляется в CRM как разметка, поэтому при сохранении он проверяется: только элементы рисунка,
// без <script>, <foreignObject>, ссылок и обработчиков событий.
public partial class FloorPlanService(AppDbContext db, IAuditService audit) : IFloorPlanService
{
    public const int MaxTemplateLength = 200_000;
    private const string BuildingNotFound = "Дом не найден";
    private const string PlanNotFound = "План этажа не задан";

    public static readonly IReadOnlyDictionary<ApartmentStatus, string> Colors = new Dictionary<ApartmentStatus, string>
    {
        [ApartmentStatus.Free] = "#22c55e",
        [ApartmentStatus.Booked] = "#f59e0b",
        [ApartmentStatus.Sold] = "#ef4444",
        [ApartmentStatus.OnRequest] = "#6b7280",
    };

    // Чем можно рисовать план; всё остальное — отказ при сохранении.
    private static readonly HashSet<string> AllowedElements =
        ["svg", "g", "rect", "text", "tspan", "title", "desc", "line", "path", "polygon", "polyline", "circle", "ellipse"];

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    [GeneratedRegex("^apt-([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$")]
    private static partial Regex ApartmentRectId();

    public async Task<IReadOnlyList<int>> FloorsWithPlanAsync(Guid buildingId, CancellationToken ct = default)
    {
        await EnsureBuildingAsync(buildingId, ct);
        return await db.FloorPlans.AsNoTracking().Where(p => p.BuildingId == buildingId)
            .OrderBy(p => p.Floor).Select(p => p.Floor).ToListAsync(ct);
    }

    public async Task<string> RenderAsync(Guid buildingId, int floor, CancellationToken ct = default)
    {
        await EnsureBuildingAsync(buildingId, ct);
        var template = await db.FloorPlans.AsNoTracking().Where(p => p.BuildingId == buildingId && p.Floor == floor)
            .Select(p => p.SvgTemplate).FirstOrDefaultAsync(ct) ?? throw new NotFoundException(PlanNotFound);

        var svg = Parse(template);
        var rects = svg.Descendants().Where(e => e.Name.LocalName == "rect")
            .Select(e => (Rect: e, Match: ApartmentRectId().Match((string?)e.Attribute("id") ?? "")))
            .Where(x => x.Match.Success)
            .Select(x => (x.Rect, Id: Guid.Parse(x.Match.Groups[1].Value)))
            .ToList();
        var ids = rects.Select(r => r.Id).ToList();

        // Только квартиры этого дома: чужой id в шаблоне остаётся серым прямоугольником без данных.
        var apartments = await db.Apartments.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Floor.BuildingId == buildingId)
            .Select(a => new { a.Id, a.Number, a.Area, a.Price, a.Currency, a.Status })
            .ToDictionaryAsync(a => a.Id, ct);
        var deals = (await db.Deals.AsNoTracking()
                .Where(d => ids.Contains(d.ApartmentId) && d.Stage != DealStage.Cancelled)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new { d.ApartmentId, d.Id })
                .ToListAsync(ct))
            .GroupBy(d => d.ApartmentId).ToDictionary(g => g.Key, g => g.First().Id);

        foreach (var (rect, id) in rects)
        {
            foreach (var old in rect.Elements().Where(e => e.Name.LocalName == "title").ToList())
                old.Remove();
            if (!apartments.TryGetValue(id, out var a))
            {
                rect.SetAttributeValue("fill", Colors[ApartmentStatus.OnRequest]);
                continue;
            }

            rect.SetAttributeValue("fill", Colors[a.Status]);
            rect.SetAttributeValue("class", "fp-apt");
            rect.SetAttributeValue("data-apartment-id", a.Id);
            rect.SetAttributeValue("data-number", a.Number);
            rect.SetAttributeValue("data-status", a.Status.ToString().ToLowerInvariant());
            if (deals.TryGetValue(id, out var dealId) && a.Status != ApartmentStatus.Free)
                rect.SetAttributeValue("data-deal-id", dealId);
            rect.Add(new XElement(rect.Name.Namespace + "title",
                $"Кв. {a.Number}, {a.Area.ToString("0.##", Russian)} м², {a.Price.ToString("#,0", Russian)} {a.Currency}"));
        }

        return svg.ToString(SaveOptions.DisableFormatting);
    }

    public async Task SaveAsync(Guid buildingId, int floor, string svgTemplate, CancellationToken ct = default)
    {
        var building = await EnsureBuildingAsync(buildingId, ct);
        if (!await db.Floors.AnyAsync(f => f.BuildingId == buildingId && f.Number == floor, ct))
            throw new NotFoundException("Этаж не найден");
        var clean = Sanitize(svgTemplate);

        var plan = await db.FloorPlans.FirstOrDefaultAsync(p => p.BuildingId == buildingId && p.Floor == floor, ct);
        var before = plan is null ? null : new { Length = plan.SvgTemplate.Length };
        if (plan is null)
        {
            plan = new FloorPlan { BuildingId = buildingId, Floor = floor };
            db.FloorPlans.Add(plan);
        }
        plan.SvgTemplate = clean;
        audit.Record(nameof(FloorPlan), plan.Id, "Updated", before, new { BuildingId = buildingId, Floor = floor, clean.Length }, building.OrganizationId);
        await db.SaveChangesAsync(ct);
    }

    // Проверенный шаблон: корень <svg>, только разрешённые элементы, без обработчиков on*, ссылок и url() в стилях.
    public static string Sanitize(string? svgTemplate)
    {
        if (string.IsNullOrWhiteSpace(svgTemplate))
            throw new BusinessRuleException("Пустой шаблон плана");
        if (svgTemplate.Length > MaxTemplateLength)
            throw new BusinessRuleException($"Шаблон плана — не больше {MaxTemplateLength / 1000} тыс. символов");

        var svg = Parse(svgTemplate);
        if (svg.Name.LocalName != "svg")
            throw new BusinessRuleException("Шаблон должен начинаться с <svg>");
        foreach (var element in svg.DescendantsAndSelf())
        {
            if (!AllowedElements.Contains(element.Name.LocalName))
                throw new BusinessRuleException($"Недопустимый элемент <{element.Name.LocalName}> в плане этажа");
            foreach (var attribute in element.Attributes().ToList())
            {
                var name = attribute.Name.LocalName.ToLowerInvariant();
                if (attribute.IsNamespaceDeclaration)
                    continue;
                if (name.StartsWith("on", StringComparison.Ordinal) || name == "href"
                    || (name == "style" && attribute.Value.Contains("url(", StringComparison.OrdinalIgnoreCase)))
                    attribute.Remove();
            }
        }
        return svg.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Parse(string svg)
    {
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(svg), settings);
            return XDocument.Load(reader).Root ?? throw new BusinessRuleException("Пустой шаблон плана");
        }
        catch (XmlException e)
        {
            throw new BusinessRuleException($"Шаблон плана — некорректный SVG (строка {e.LineNumber}, позиция {e.LinePosition})");
        }
    }

    private async Task<Building> EnsureBuildingAsync(Guid buildingId, CancellationToken ct) =>
        await db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct)
            ?? throw new NotFoundException(BuildingNotFound);
}
