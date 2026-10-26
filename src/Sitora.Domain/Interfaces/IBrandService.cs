using Sitora.Domain.DTOs.Brand;

namespace Sitora.Domain.Interfaces;

public interface IBrandService
{
    // Бренд по хосту сайта (поддомен платформы или свой домен) или по короткому имени; 404, если такого нет.
    Task<PublicBrandResponse> GetPublicAsync(string? host, string? slug, CancellationToken ct = default);

    Task<LogoFile> OpenLogoAsync(Guid organizationId, CancellationToken ct = default);

    // Бренд своей организации (владелец).
    Task<BrandResponse> GetOwnAsync(CancellationToken ct = default);
    Task<BrandResponse> UpdateOwnAsync(UpdateBrandRequest request, CancellationToken ct = default);
    Task<BrandResponse> UploadLogoAsync(string fileName, long length, Stream content, CancellationToken ct = default);
    Task<BrandResponse> DeleteLogoAsync(CancellationToken ct = default);

    // Создатель платформы: короткое имя и свой домен любой организации.
    Task<BrandResponse> SetDomainAsync(Guid organizationId, UpdateOrganizationDomainRequest request, CancellationToken ct = default);

    // Название для писем: бренд организации сотрудника или покупателя; без организации — «Sitora».
    Task<string> NameForEmailAsync(string email, CancellationToken ct = default);
}
