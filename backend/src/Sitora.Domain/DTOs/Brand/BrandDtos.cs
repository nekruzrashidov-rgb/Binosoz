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
    string? CustomDomain,
    string? Email = null,
    decimal? Latitude = null,
    decimal? Longitude = null);

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
    IReadOnlyList<BuildingResponse> Buildings,
    string? Email = null,
    // План участка ЖК (контур, двор, площадки); не задан — поля нет.
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    Sitora.Domain.Entities.SitePlan? SitePlan = null,
    // Точка ЖК для карты: своя у организации или центр опубликованных домов с координатами; нет — полей нет.
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    decimal? Latitude = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    decimal? Longitude = null,
    // Ключ Google Maps Embed API (Maps__EmbedKey): публичный по природе, ограничивается в Google Cloud; не задан — поля нет.
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? MapsEmbedKey = null);

// Точка ЖК на карте (CRM → «Настройки → Компания на сайте»): широта и долгота парой, пусто — убрать.
public record SaveOrganizationLocationRequest(decimal? Latitude = null, decimal? Longitude = null);

// Владелец меняет название, короткое имя (поддомен), цвет и контакты. Свой домен задаёт создатель платформы.
public record UpdateBrandRequest(string Name, string Slug, string? PrimaryColor, string? Phone, string? Telegram, string? Address, string? Email = null);

// Создатель платформы: короткое имя и свой домен организации (null — без своего домена).
public record UpdateOrganizationDomainRequest(string Slug, string? CustomDomain);

public record LogoFile(Stream Content, string ContentType);
