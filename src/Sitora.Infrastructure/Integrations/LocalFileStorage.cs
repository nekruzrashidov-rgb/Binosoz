using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Файлы лежат в папке Storage:RootPath (по умолчанию ./storage рядом с приложением).
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Storage:RootPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "storage")
            : configured);
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (!File.Exists(path))
            throw new NotFoundException("Файл не найден");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    // Ключ не должен выводить за пределы корневой папки.
    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Недопустимый ключ файла");
        return path;
    }
}
