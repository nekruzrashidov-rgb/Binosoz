using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    // Storage__S3__UnsignedPayload: тело PutObject без подписи SigV4 (UNSIGNED-PAYLOAD) и без контрольной суммы SDK.
    // Cloudflare R2 не понимает потоковую подпись SDK v4 (aws-chunked, STREAMING-…-PAYLOAD-TRAILER) — без этого запись
    // в R2 падает. Пусто — авто: да для своего https-эндпоинта (R2, B2), нет для AWS S3 (пустой Endpoint). По http
    // всегда нет: SDK запрещает unsigned payload без TLS (MinIO в docker compose и тестах). Целостность при «да» — TLS.
    public bool? UnsignedPayload { get; set; }

    // Storage__S3__ChecksumWhenRequired: контрольные суммы (CRC32 и др.) — только там, где их требует операция
    // (WHEN_REQUIRED), а не всегда (WHEN_SUPPORTED — умолчание SDK v4). Пусто — авто: да для любого своего эндпоинта
    // (S3-совместимые сервисы отстают от новых заголовков AWS), нет для AWS S3.
    public bool? ChecksumWhenRequired { get; set; }

    // Storage__S3__ConditionalWrites: запрет перезаписи заголовком If-None-Match: * (R2, AWS S3, MinIO его поддерживают).
    // false — без заголовка: ключи файлов и так уникальны (Guid). Ответ 501 NotImplemented отключает заголовок сам.
    public bool ConditionalWrites { get; set; } = true;

    public bool HasCustomEndpoint => !string.IsNullOrWhiteSpace(Endpoint);

    public bool IsHttpsEndpoint =>
        HasCustomEndpoint && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    // Без своего эндпоинта (AWS S3) соединение тоже https: явное UnsignedPayload=true там допустимо.
    public bool EffectiveUnsignedPayload => HasCustomEndpoint
        ? IsHttpsEndpoint && (UnsignedPayload ?? true)
        : UnsignedPayload ?? false;

    public bool EffectiveChecksumWhenRequired => ChecksumWhenRequired ?? HasCustomEndpoint;
}

// Файлы в S3-совместимом хранилище. Подписанные ссылки /api/v1/files?token=… работают как с диском:
// API сам читает объект и отдаёт его, ключи и доступы к бакету наружу не попадают.
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly bool _unsignedPayload;
    private readonly ILogger _logger;
    private volatile bool _conditionalWrites;

    public S3FileStorage(IOptions<StorageOptions> options, ILogger<S3FileStorage>? logger = null)
    {
        var o = options.Value.S3;
        if (string.IsNullOrWhiteSpace(o.Bucket) || string.IsNullOrWhiteSpace(o.AccessKey) || string.IsNullOrWhiteSpace(o.SecretKey))
            throw new InvalidOperationException("Для Storage__Provider=S3 задайте Storage__S3__Bucket, Storage__S3__AccessKey и Storage__S3__SecretKey");

        _client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), CreateClientConfig(o));
        _bucket = o.Bucket;
        _unsignedPayload = o.EffectiveUnsignedPayload;
        _conditionalWrites = o.ConditionalWrites;
        _logger = logger ?? NullLogger<S3FileStorage>.Instance;
    }

    public bool ConditionalWritesEnabled => _conditionalWrites;

    public static AmazonS3Config CreateClientConfig(S3StorageOptions o)
    {
        var config = new AmazonS3Config { ForcePathStyle = o.ForcePathStyle };
        if (o.HasCustomEndpoint)
        {
            config.ServiceURL = o.Endpoint;
            config.AuthenticationRegion = o.Region;
        }
        else
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);

        if (o.EffectiveChecksumWhenRequired)
        {
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
            config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
        }
        return config;
    }

    public PutObjectRequest CreatePutRequest(string key, Stream body, bool conditional)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = Normalize(key),
            InputStream = body,
            AutoCloseStream = false
        };
        // Как FileMode.CreateNew у диска: существующий объект не перезаписывается.
        if (conditional)
            request.IfNoneMatch = "*";
        if (_unsignedPayload)
        {
            request.DisablePayloadSigning = true;
            request.DisableDefaultChecksumValidation = true;
        }
        return request;
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

        var conditional = _conditionalWrites;
        var start = body.Position;
        try
        {
            await _client.PutObjectAsync(CreatePutRequest(key, body, conditional), ct);
        }
        catch (AmazonS3Exception e) when (conditional && e.StatusCode == HttpStatusCode.NotImplemented)
        {
            // Сервис не умеет условную запись: дальше без If-None-Match (ключи файлов уникальны — Guid).
            _conditionalWrites = false;
            _logger.LogWarning("Хранилище S3 не поддерживает If-None-Match ({ErrorCode}): запись без запрета перезаписи", e.ErrorCode);
            body.Position = start;
            await _client.PutObjectAsync(CreatePutRequest(key, body, conditional: false), ct);
        }
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
