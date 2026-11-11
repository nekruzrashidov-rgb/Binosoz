using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Где лежат файлы: Local — папка на диске (dev, тесты, VPS с томом), S3 — объектное хранилище (MinIO, Cloudflare R2,
// Backblaze B2, AWS S3). На Render файловая система временная: там нужен S3 (или Render Disk с Storage__RootPath).
public class StorageOptions
{
    public const string SectionName = "Storage";
    public const string LocalProvider = "Local";
    public const string S3Provider = "S3";

    public string Provider { get; set; } = LocalProvider;
    public string? RootPath { get; set; }
    public S3StorageOptions S3 { get; set; } = new();

    public bool IsS3 => string.Equals(Provider, S3Provider, StringComparison.OrdinalIgnoreCase);
}

public class S3StorageOptions
{
    // Адрес S3-совместимого сервиса, например http://minio:9000 или https://<account>.r2.cloudflarestorage.com.
    // Пусто — AWS S3 в регионе Region.
    public string? Endpoint { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    // MinIO и B2 адресуют бакет путём (http://host/bucket/key), а не поддоменом.
    public bool ForcePathStyle { get; set; } = true;
}

// Файлы в S3-совместимом хранилище. Подписанные ссылки /api/v1/files?token=… работают как с диском:
// API сам читает объект и отдаёт его, ключи и доступы к бакету наружу не попадают.
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        var o = options.Value.S3;
        if (string.IsNullOrWhiteSpace(o.Bucket) || string.IsNullOrWhiteSpace(o.AccessKey) || string.IsNullOrWhiteSpace(o.SecretKey))
            throw new InvalidOperationException("Для Storage__Provider=S3 задайте Storage__S3__Bucket, Storage__S3__AccessKey и Storage__S3__SecretKey");

        var config = new AmazonS3Config { ForcePathStyle = o.ForcePathStyle };
        if (string.IsNullOrWhiteSpace(o.Endpoint))
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);
        else
        {
            config.ServiceURL = o.Endpoint;
            config.AuthenticationRegion = o.Region;
        }
        _client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
        _bucket = o.Bucket;
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        // S3 нужна длина тела: поток без длины (загрузка из формы) сначала копируется в память.
        // Размер файлов ограничен на входе (документы — до 20 МБ).
        Stream body = content;
        if (!content.CanSeek)
        {
            var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            body = buffer;
        }

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = Normalize(key),
            InputStream = body,
            AutoCloseStream = false,
            // Как FileMode.CreateNew у диска: существующий объект не перезаписывается.
            IfNoneMatch = "*"
        }, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, Normalize(key), ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Файл не найден");
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucket, Normalize(key), ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Удалять уже нечего — как File.Delete у диска.
        }
    }

    // Ключ — относительный путь без «..»: как у диска, он не выходит за пределы своего хранилища.
    private static string Normalize(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(part => part is ".." or "."))
            throw new InvalidOperationException("Недопустимый ключ файла");
        return normalized;
    }

    public void Dispose() => _client.Dispose();
}
