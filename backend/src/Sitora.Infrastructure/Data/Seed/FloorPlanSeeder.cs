using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Seed;

// Простой план 1-го этажа «Некруз Резиденс» для разработки: четыре квартиры в ряд, прямоугольники 120×80 с номерами.
public static class FloorPlanSeeder
{
    public const int Floor = 1;
    private const int Width = 120, Height = 80, Gap = 16, Padding = 20;

    public static async Task SeedAsync(AppDbContext db)
    {
        var buildingId = await db.Buildings.Where(b => b.NameRu == CatalogSeeder.BuildingName).Select(b => (Guid?)b.Id).FirstOrDefaultAsync();
        if (buildingId is null || await db.FloorPlans.AnyAsync(p => p.BuildingId == buildingId && p.Floor == Floor))
            return;
        var apartments = await db.Apartments
            .Where(a => a.Floor.BuildingId == buildingId && a.Floor.Number == Floor)
            .OrderBy(a => a.PlanSlot).Take(4)
            .Select(a => new { a.Id, a.Number })
            .ToListAsync();
        if (apartments.Count == 0)
            return;

        db.FloorPlans.Add(new FloorPlan { BuildingId = buildingId.Value, Floor = Floor, SvgTemplate = Svg(apartments.Select(a => (a.Id, a.Number)).ToList()) });
        await db.SaveChangesAsync();
    }

    public static string Svg(IReadOnlyList<(Guid Id, int Number)> apartments)
    {
        var width = Padding * 2 + apartments.Count * Width + (apartments.Count - 1) * Gap;
        var height = Padding * 2 + Height;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\">");
        svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"4\" y=\"4\" width=\"{width - 8}\" height=\"{height - 8}\" rx=\"8\" fill=\"none\" stroke=\"#94a3b8\" stroke-width=\"2\"/>");
        for (var i = 0; i < apartments.Count; i++)
        {
            var x = Padding + i * (Width + Gap);
            svg.Append(CultureInfo.InvariantCulture, $"<rect id=\"apt-{apartments[i].Id}\" x=\"{x}\" y=\"{Padding}\" width=\"{Width}\" height=\"{Height}\" rx=\"6\" stroke=\"#0f172a\" stroke-width=\"1.5\"/>");
            svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + Width / 2}\" y=\"{Padding + Height / 2 + 6}\" text-anchor=\"middle\" font-size=\"18\" font-weight=\"700\" fill=\"#0f172a\" pointer-events=\"none\">{apartments[i].Number}</text>");
        }
        return svg.Append("</svg>").ToString();
    }
}
