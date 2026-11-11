using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Sitora.Domain.Exceptions;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Tests.Infrastructure;

// S3FileStorage на настоящем MinIO (Testcontainers, образ quay.io/minio/minio): запись, чтение, отказ перезаписать, удаление.
// Без Docker (локальный прогон через SITORA_TEST_DB) тест помечается пропущенным, в CI Docker есть.
public class S3FileStorageTests : IAsyncLifetime
{
    private const string User = "sitora-test";
    private readonly string _password = Guid.NewGuid().ToString("N");
    private IContainer? _minio;
    private string? _skipReason;

    public async Task InitializeAsync()
    {
        try
        {
            _minio = new ContainerBuilder("quay.io/minio/minio:RELEASE.2025-04-22T22-12-26Z")
                .WithCommand("server", "/data")
                .WithEnvironment("MINIO_ROOT_USER", User)
                .WithEnvironment("MINIO_ROOT_PASSWORD", _password)
                .WithPortBinding(9000, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/minio/health/live")))
                .Build();
            await _minio.StartAsync();
        }
        catch (Exception e) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            _skipReason = "Docker недоступен: " + e.GetType().Name;
        }
    }

    public async Task DisposeAsync()
    {
        if (_minio is not null)
            await _minio.DisposeAsync();
    }

    [SkippableFact]
    public async Task SaveReadDelete_WorkOnMinio()
    {
        Skip.If(_skipReason is not null, _skipReason);
        var endpoint = $"http://{_minio!.Hostname}:{_minio.GetMappedPublicPort(9000)}";
        const string bucket = "sitora-files";
        using (var admin = new AmazonS3Client(new BasicAWSCredentials(User, _password), new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" }))
            await admin.PutBucketAsync(bucket);

        using var storage = new S3FileStorage(Options.Create(new StorageOptions
        {
            Provider = StorageOptions.S3Provider,
            S3 = new S3StorageOptions { Endpoint = endpoint, Bucket = bucket, AccessKey = User, SecretKey = _password }
        }));

        var key = $"documents/{Guid.NewGuid():N}/договор.pdf";
        byte[] content = [0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37];
        await storage.SaveAsync(key, new NonSeekableStream(content));

        await using (var read = await storage.OpenReadAsync(key))
        {
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }

        // Как у диска (FileMode.CreateNew): существующий файл не перезаписывается.
        await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => storage.SaveAsync(key, new MemoryStream([1])));
        // Ключ не выходит за пределы хранилища.
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync("../secret", new MemoryStream([1])));

        await storage.DeleteAsync(key);
        await Assert.ThrowsAsync<NotFoundException>(() => storage.OpenReadAsync(key));
        // Повторное удаление — не ошибка, как File.Delete.
        await storage.DeleteAsync(key);
    }

    // Загрузка из формы: поток без длины и без перемотки.
    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
