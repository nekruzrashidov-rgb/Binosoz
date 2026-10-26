using Sitora.Domain.DTOs.Catalog;

namespace Sitora.Domain.DTOs.Brand;

// Бренд застройщика: вывеска, цвет и контакты для его сайта и писем.
public record BrandResponse(
    Guid OrganizationId,
    string Name,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    string? Phone,
    string? Telegram,
    string? Address,
    string? CustomDomain);

// Публичный ответ для сайта: бренд и только дома этого застройщика.
public record PublicBrandResponse(
    Guid OrganizationId,
    string Name,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    string? Phone,
    string? Telegram,
    string? Address,
    IReadOnlyList<BuildingResponse> Buildings);

// Владелец меняет название, короткое имя (поддомен), цвет и контакты. Свой домен задаёт создатель платформы.
public record UpdateBrandRequest(string Name, string Slug, string? PrimaryColor, string? Phone, string? Telegram, string? Address);

// Создатель платформы: короткое имя и свой домен организации (null — без своего домена).
public record UpdateOrganizationDomainRequest(string Slug, string? CustomDomain);

public record LogoFile(Stream Content, string ContentType);
