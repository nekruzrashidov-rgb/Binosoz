using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Brand;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Публичная вывеска сайта застройщика (white-label): вход не нужен.
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicController(IBrandService brands) : ControllerBase
{
    /// <summary>
    /// Бренд и дома застройщика по хосту сайта (richtower.sitora.tj или свой домен) либо по короткому имени (slug).
    /// Хост важнее slug. Неизвестный хост — 404; дома другого застройщика сюда не попадают.
    /// </summary>
    [HttpGet("brand")]
    [ProducesResponseType<PublicBrandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PublicBrandResponse> Brand(string? host, string? slug, CancellationToken ct) =>
        brands.GetPublicAsync(host, slug, ct);

    /// <summary>Логотип организации; адрес с номером версии, поэтому файл можно кэшировать.</summary>
    [HttpGet("organizations/{organizationId:guid}/logo")]
    [Produces("image/png", "image/jpeg")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logo(Guid organizationId, CancellationToken ct)
    {
        var logo = await brands.OpenLogoAsync(organizationId, ct);
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(logo.Content, logo.ContentType);
    }
}

// Раздел владельца «Бренд и сайт».
[ApiController]
[Route("api/v1/owner/brand")]
[Authorize(Roles = Roles.OrganizationLeaders)]
[Produces("application/json")]
public class OwnerBrandController(IBrandService brands) : ControllerBase
{
    [HttpGet]
    public Task<BrandResponse> Get(CancellationToken ct) => brands.GetOwnAsync(ct);

    /// <summary>
    /// Название, короткое имя (поддомен), цвет #RRGGBB и контакты. Короткое имя: латиница, цифры и дефис,
    /// уникально, admin, www, api и другие служебные имена запрещены.
    /// </summary>
    [HttpPut]
    [ProducesResponseType<BrandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<BrandResponse> Update(UpdateBrandRequest request, CancellationToken ct) => brands.UpdateOwnAsync(request, ct);

    /// <summary>Логотип: PNG или JPG до 2 МБ.</summary>
    [HttpPost("logo")]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<BrandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<BrandResponse> UploadLogo(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await brands.UploadLogoAsync(file.FileName, file.Length, stream, ct);
    }

    [HttpDelete("logo")]
    public Task<BrandResponse> DeleteLogo(CancellationToken ct) => brands.DeleteLogoAsync(ct);
}
