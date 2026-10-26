using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Brand;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Домены платформы, на которых живут поддомены застройщиков (раздел Brand; переменные Brand__BaseDomains__0 …).
public class BrandOptions
{
    public const string SectionName = "Brand";

    public string[] BaseDomains { get; set; } = ["sitora.tj", "localhost"];
}

// Бренд застройщика (white-label): публичная вывеска для сайта, правка владельцем, домен — создателем платформы.
public class BrandService(
    AppDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditService audit,
    CatalogCache catalogCache,
    TimeProvider timeProvider,
    IOptions<BrandOptions> options) : IBrandService
{
    public const string PlatformName = "Sitora";
    private const long MaxLogoSize = 2 * 1024 * 1024;
    private const string NotFound = "Сайт застройщика не найден";

    public async Task<PublicBrandResponse> GetPublicAsync(string? host, string? slug, CancellationToken ct = default)
    {
        var organization = await FindByHostOrSlugAsync(host, slug, ct) ?? throw new NotFoundException(NotFound);

        // Дома выбираются по id организации, без общего кэша каталога: короткое имя может перейти к другому
        // застройщику, и закэшированный по нему список не должен показаться на чужом сайте.
        var tg = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";
        var buildings = await db.Buildings.AsNoTracking()
            .Where(b => b.OrganizationId == organization.Id)
            .OrderBy(b => b.NameRu).ThenBy(b => b.Id)
            .Select(CatalogService.ToBuildingResponse(tg))
            .ToListAsync(ct);

        return new PublicBrandResponse(organization.Id, organization.Name, organization.Slug, organization.LogoUrl,
            organization.PrimaryColor, organization.Phone, organization.Telegram, organization.Address, buildings, organization.Email);
    }

    // Свой домен проверяется первым; иначе нужен ровно один уровень под доменом платформы: slug.sitora.tj.
    private async Task<Organization?> FindByHostOrSlugAsync(string? host, string? slug, CancellationToken ct)
    {
        var active = db.Organizations.AsNoTracking().Where(o => o.IsActive);
        var normalizedHost = BrandRules.NormalizeHost(host);
        if (normalizedHost is not null)
        {
            var byDomain = await active.FirstOrDefaultAsync(o => o.CustomDomain == normalizedHost, ct);
            if (byDomain is not null)
                return byDomain;

            var sub = options.Value.BaseDomains
                .Select(d => d.Trim().ToLowerInvariant())
                .Where(d => normalizedHost.EndsWith("." + d, StringComparison.Ordinal))
                .Select(d => normalizedHost[..^(d.Length + 1)])
                .FirstOrDefault(s => !s.Contains('.'));
            return BrandRules.NormalizeSlug(sub) is { } fromHost && !BrandRules.IsReserved(fromHost)
                ? await active.FirstOrDefaultAsync(o => o.Slug == fromHost, ct)
                : null;
        }

        return BrandRules.NormalizeSlug(slug) is { } bySlug
            ? await active.FirstOrDefaultAsync(o => o.Slug == bySlug, ct)
            : null;
    }

    public async Task<LogoFile> OpenLogoAsync(Guid organizationId, CancellationToken ct = default)
    {
        var logo = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId && o.IsActive && o.LogoFileKey != null)
            .Select(o => new { o.LogoFileKey, o.LogoContentType })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Логотип не найден");
        return new LogoFile(await storage.OpenReadAsync(logo.LogoFileKey!, ct), logo.LogoContentType ?? "image/png");
    }

    public async Task<BrandResponse> GetOwnAsync(CancellationToken ct = default) =>
        ToResponse(await OwnAsync(ct));

    public async Task<BrandResponse> UpdateOwnAsync(UpdateBrandRequest request, CancellationToken ct = default)
    {
        var organization = await OwnAsync(ct);
        var before = ToResponse(organization);

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
            throw new BusinessRuleException("Название — от 1 до 200 символов");
        var slug = await CheckSlugAsync(request.Slug, organization.Id, ct);
        string? color = null;
        if (!string.IsNullOrWhiteSpace(request.PrimaryColor))
        {
            color = request.PrimaryColor.Trim().ToLowerInvariant();
            if (!BrandRules.IsColor(color))
                throw new BusinessRuleException("Цвет — в формате #RRGGBB, например #1f6feb");
        }

        organization.Name = name;
        organization.Slug = slug;
        organization.PrimaryColor = color;
        organization.Phone = Clean(request.Phone, 32, "Телефон");
        organization.Telegram = Clean(request.Telegram, 64, "Telegram");
        organization.Address = Clean(request.Address, 300, "Адрес");
        organization.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты для связи");

        audit.Record(nameof(Organization), organization.Id, "BrandUpdated", before, ToResponse(organization), organization.Id);
        await SaveAsync(ct);
        return ToResponse(organization);
    }

    public async Task<BrandResponse> UploadLogoAsync(string fileName, long length, Stream content, CancellationToken ct = default)
    {
        var organization = await OwnAsync(ct);
        var (buffer, contentType, extension) = await FileTypes.ReadCheckedAsync(FileTypes.Images, fileName, length, content,
            MaxLogoSize, "PNG, JPG", ct);
        await using var _ = buffer;

        var oldKey = organization.LogoFileKey;
        var version = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var key = $"brand/{organization.Id:N}/logo-{version}{extension}";
        await storage.SaveAsync(key, buffer, ct);

        organization.LogoFileKey = key;
        organization.LogoContentType = contentType;
        // Номер версии в адресе: браузеры и прокси держат логотип в кэше, новый файл — новый адрес.
        organization.LogoUrl = $"/api/v1/public/organizations/{organization.Id}/logo?v={version}";
        audit.Record(nameof(Organization), organization.Id, "LogoUploaded", null, new { organization.LogoUrl }, organization.Id);
        try
        {
            await SaveAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        if (oldKey is not null)
            await storage.DeleteAsync(oldKey, CancellationToken.None);
        return ToResponse(organization);
    }

    public async Task<BrandResponse> DeleteLogoAsync(CancellationToken ct = default)
    {
        var organization = await OwnAsync(ct);
        var oldKey = organization.LogoFileKey;
        organization.LogoFileKey = null;
        organization.LogoContentType = null;
        organization.LogoUrl = null;
        audit.Record(nameof(Organization), organization.Id, "LogoDeleted", null, null, organization.Id);
        await SaveAsync(ct);
        if (oldKey is not null)
            await storage.DeleteAsync(oldKey, CancellationToken.None);
        return ToResponse(organization);
    }

    public async Task<BrandResponse> SetDomainAsync(Guid organizationId, UpdateOrganizationDomainRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsInRole(RoleNames.PlatformOwner))
            throw new ForbiddenException("Доступно только создателю платформы");
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
        var before = ToResponse(organization);

        organization.Slug = await CheckSlugAsync(request.Slug, organization.Id, ct);
        string? domain = null;
        if (!string.IsNullOrWhiteSpace(request.CustomDomain))
        {
            domain = BrandRules.NormalizeDomain(request.CustomDomain)
                ?? throw new BusinessRuleException("Домен — например richtower.tj, без https:// и пути");
            // Поддомены платформы раздаются по короткому имени, свой домен должен быть чужим ей.
            if (options.Value.BaseDomains.Any(d => domain == d || domain.EndsWith("." + d, StringComparison.Ordinal)))
                throw new BusinessRuleException("Поддомен платформы задаётся коротким именем, а не своим доменом");
            if (await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.CustomDomain == domain && o.Id != organization.Id, ct))
                throw new ConflictException("Этот домен уже занят другой организацией");
        }

        organization.CustomDomain = domain;
        audit.Record(nameof(Organization), organization.Id, "DomainUpdated", before, ToResponse(organization), organization.Id);
        await SaveAsync(ct);
        return ToResponse(organization);
    }

    public async Task<string> NameForEmailAsync(string email, CancellationToken ct = default)
    {
        var staffOrganization = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Email == email && u.OrganizationId != null)
            .Select(u => u.Organization!.Name)
            .FirstOrDefaultAsync(ct);
        if (staffOrganization is not null)
            return staffOrganization;

        // Покупатель: застройщик его последней сделки, а без сделок — последней карточки клиента.
        var customerOrganization = await db.Customers.IgnoreQueryFilters()
            .Where(c => c.Email == email)
            .OrderByDescending(c => db.Deals.IgnoreQueryFilters().Where(d => d.CustomerId == c.Id).Max(d => (DateTime?)d.CreatedAt) ?? c.CreatedAt)
            .Select(c => db.Organizations.IgnoreQueryFilters().Where(o => o.Id == c.OrganizationId).Select(o => o.Name).FirstOrDefault())
            .FirstOrDefaultAsync(ct);
        return customerOrganization ?? PlatformName;
    }

    private async Task<string> CheckSlugAsync(string? value, Guid organizationId, CancellationToken ct)
    {
        var slug = BrandRules.NormalizeSlug(value) ?? throw new BusinessRuleException(BrandRules.SlugMessage);
        if (BrandRules.IsReserved(slug))
            throw new BusinessRuleException($"Короткое имя «{slug}» зарезервировано платформой, выберите другое");
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Slug == slug && o.Id != organizationId, ct))
            throw new ConflictException("Это короткое имя уже занято другой организацией");
        return slug;
    }

    private async Task<Organization> OwnAsync(CancellationToken ct)
    {
        var organizationId = currentUser.OrganizationId
            ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец");
        return await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
               ?? throw new NotFoundException("Организация не найдена");
    }

    // Короткое имя и вывеска видны в публичном каталоге: после изменения кэш каталога сбрасывается.
    private async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        catalogCache.Invalidate();
    }

    private static string? Clean(string? value, int max, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > max)
            throw new BusinessRuleException($"{field} — не длиннее {max} символов");
        return trimmed;
    }

    private static BrandResponse ToResponse(Organization o) =>
        new(o.Id, o.Name, o.Slug, o.LogoUrl, o.PrimaryColor, o.Phone, o.Telegram, o.Address, o.CustomDomain, o.Email);
}
