using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Планировки застройщика (JSON из CRM): проверка формата и геометрии, сохранение с AuditLog, публичный список для сайта.
public class LayoutService(AppDbContext db, ICurrentUser currentUser, IAuditService audit, CatalogCache cache)
{
    private Guid Organization => currentUser.OrganizationId ?? throw new NotFoundException("Организация не найдена");

    public async Task<IReadOnlyList<LayoutResponse>> GetOwnAsync(CancellationToken ct = default)
    {
        var org = Organization;
        return (await db.Layouts.AsNoTracking().Where(l => l.OrganizationId == org).OrderBy(l => l.Code).ToListAsync(ct)).Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<LayoutResponse>> GetPublicAsync(string slug, CancellationToken ct = default)
    {
        var s = slug.Trim().ToLowerInvariant();
        var org = await db.Organizations.AsNoTracking().Where(o => o.Slug == s && o.IsActive).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Организация не найдена");
        return (await db.Layouts.IgnoreQueryFilters().AsNoTracking().Where(l => l.OrganizationId == org).OrderBy(l => l.Code).ToListAsync(ct)).Select(ToResponse).ToList();
    }

    public async Task<LayoutResponse> SaveAsync(string code, SaveLayoutRequest request, CancellationToken ct = default)
    {
        var org = Organization;
        var key = NormalizeCode(code);
        Validate(request.Layout);
        var layout = await db.Layouts.FirstOrDefaultAsync(l => l.OrganizationId == org && l.Code == key, ct);
        var json = request.Layout.GetRawText();
        var before = layout is null ? null : new { layout.Name, layout.Json };
        if (layout is null)
        {
            layout = new Layout { OrganizationId = org, Code = key };
            db.Layouts.Add(layout);
        }
        layout.Name = string.IsNullOrWhiteSpace(request.Name) ? key : request.Name.Trim();
        layout.Json = json;
        audit.Record(nameof(Layout), layout.Id, before is null ? "Created" : "Updated", before, new { layout.Code, layout.Name, Json = json }, org);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(ct);
        return ToResponse(layout);
    }

    public async Task DeleteAsync(string code, CancellationToken ct = default)
    {
        var org = Organization;
        var key = NormalizeCode(code);
        var layout = await db.Layouts.FirstOrDefaultAsync(l => l.OrganizationId == org && l.Code == key, ct)
            ?? throw new NotFoundException("Планировка не найдена");
        audit.Record(nameof(Layout), layout.Id, "Deleted", new { layout.Code, layout.Name }, null, org);
        db.Layouts.Remove(layout);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(ct);
    }

    public static string NormalizeCode(string code)
    {
        var key = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length is < 1 or > Layout.CodeMaxLength || !key.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            throw new BadRequestException("Код планировки — от 1 до 32 латинских букв, цифр, дефисов");
        return key;
    }

    // Формат сайта: outline — контур (3–64 вершины, без самопересечений), rooms — [[ключ, многоугольник], …] (1–30 комнат,
    // каждая простая и не меньше 0,5 м²), сумма комнат — от 10 до 1 000 м², комнаты внутри контура; balcony.poly — тоже.
    public static void Validate(JsonElement layout)
    {
        static List<double[]>? Poly(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<double[]>();
            foreach (var p in e.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Array || p.GetArrayLength() < 2 || !p.EnumerateArray().All(v => v.ValueKind == JsonValueKind.Number))
                    return null;
                list.Add([p[0].GetDouble(), p[1].GetDouble()]);
            }
            return list.Count is >= PlanGeometry.MinVertices and <= PlanGeometry.MaxVertices ? list : null;
        }
        static bool Good(List<double[]>? poly) => poly is not null && poly.All(p => p.All(double.IsFinite) && Math.Abs(p[0]) <= 200 && Math.Abs(p[1]) <= 200)
            && PlanGeometry.IsSimple(PlanGeometry.Vertices(poly)) && PlanGeometry.Area(PlanGeometry.Vertices(poly)) >= 0.5;

        if (layout.ValueKind != JsonValueKind.Object)
            throw new BadRequestException("Планировка — объект JSON с контуром и комнатами");
        if (!layout.TryGetProperty("outline", out var outlineEl) || Poly(outlineEl) is not { } outline || !Good(outline))
            throw new BadRequestException("Контур планировки — от 3 до 64 вершин, без самопересечений");
        if (!layout.TryGetProperty("rooms", out var roomsEl) || roomsEl.ValueKind != JsonValueKind.Array || roomsEl.GetArrayLength() is < 1 or > 30)
            throw new BadRequestException("В планировке — от 1 до 30 комнат");
        var ring = PlanGeometry.Vertices(outline);
        double total = 0;
        foreach (var r in roomsEl.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Array || r.GetArrayLength() != 2 || r[0].ValueKind != JsonValueKind.String || Poly(r[1]) is not { } poly || !Good(poly))
                throw new BadRequestException("Комната планировки — [название, многоугольник] без самопересечений, не меньше 0,5 м²");
            var verts = PlanGeometry.Vertices(poly);
            if (!PlanGeometry.Inside(ring, verts, 0.05))
                throw new BadRequestException("Комната планировки выходит за её контур");
            total += PlanGeometry.Area(verts);
        }
        if (total is < 10 or > 1000)
            throw new BadRequestException("Площадь комнат планировки — от 10 до 1 000 м²");
        if (layout.TryGetProperty("balcony", out var bal) && bal.ValueKind == JsonValueKind.Object
            && (!bal.TryGetProperty("poly", out var bp) || !Good(Poly(bp))))
            throw new BadRequestException("Балкон планировки — многоугольник без самопересечений");
    }

    private static LayoutResponse ToResponse(Layout l) => new(l.Code, l.Name, JsonDocument.Parse(l.Json).RootElement.Clone(), l.UpdatedAt);
}
