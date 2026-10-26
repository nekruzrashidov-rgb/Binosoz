using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Скачивание по подписанной ссылке (документы, фото стройки). Вход не нужен:
// права проверены, когда ссылку выдали, а сама ссылка живёт 15 минут.
[ApiController]
[Route("api/v1/files")]
[AllowAnonymous]
public class FilesController(IFileLinkService files) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] string? token, CancellationToken ct)
    {
        var file = await files.OpenAsync(token, ct);
        // Ссылка временная — браузер и прокси не должны хранить файл дольше неё.
        Response.Headers.CacheControl = "private, no-store";
        return file.Inline
            ? File(file.Content, file.ContentType)
            : File(file.Content, file.ContentType, file.FileName);
    }
}
