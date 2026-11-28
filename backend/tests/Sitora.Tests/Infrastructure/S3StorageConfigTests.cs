using Amazon.Runtime;
using Microsoft.Extensions.Options;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Tests.Infrastructure;

// Настройки клиента S3 без сервера: Cloudflare R2 (https) не понимает потоковую подпись и CRC32-трейлер SDK v4,
// MinIO по http должен работать как раньше (SDK запрещает unsigned payload без TLS), AWS S3 — с умолчаниями SDK.
public class S3StorageConfigTests
{
    private const string R2 = "https://0123456789abcdef.r2.cloudflarestorage.com";
    private const string MinIo = "http://localhost:9000";

    private static S3StorageOptions Options(string? endpoint, string region = "auto") => new()
    {
        Endpoint = endpoint, Bucket = "binosoz-files", AccessKey = "key", SecretKey = "secret", Region = region
    };

    private static S3FileStorage Storage(S3StorageOptions o) =>
        new(Microsoft.Extensions.Options.Options.Create(new StorageOptions { Provider = StorageOptions.S3Provider, S3 = o }));

    [Fact]
    public void R2_Https_UsesUnsignedPayloadAndChecksumsOnlyWhenRequired()
    {
        var o = Options(R2);
        var config = S3FileStorage.CreateClientConfig(o);
        Assert.Equal(RequestChecksumCalculation.WHEN_REQUIRED, config.RequestChecksumCalculation);
        Assert.Equal(ResponseChecksumValidation.WHEN_REQUIRED, config.ResponseChecksumValidation);
        Assert.True(config.ForcePathStyle);
        Assert.Equal("auto", config.AuthenticationRegion);

        using var storage = Storage(o);
        var request = storage.CreatePutRequest("documents/a.pdf", new MemoryStream([1]), conditional: true);
        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.Equal("*", request.IfNoneMatch);
    }

    [Fact]
    public void MinIo_Http_KeepsSignedPayload()
    {
        var o = Options(MinIo, "us-east-1");
        using var storage = Storage(o);
        var request = storage.CreatePutRequest("documents/a.pdf", new MemoryStream([1]), conditional: true);
        Assert.NotEqual(true, request.DisablePayloadSigning);
        Assert.NotEqual(true, request.DisableDefaultChecksumValidation);
        Assert.Equal("*", request.IfNoneMatch);
        // Даже явное UnsignedPayload=true по http не включается: SDK отказал бы в запросе.
        o.UnsignedPayload = true;
        Assert.False(o.EffectiveUnsignedPayload);
        Assert.Equal(RequestChecksumCalculation.WHEN_REQUIRED, S3FileStorage.CreateClientConfig(o).RequestChecksumCalculation);
    }

    [Fact]
    public void AwsS3_KeepsSdkDefaults()
    {
        var o = Options(null, "eu-central-1");
        var config = S3FileStorage.CreateClientConfig(o);
        Assert.Equal(RequestChecksumCalculation.WHEN_SUPPORTED, config.RequestChecksumCalculation);
        Assert.Equal(ResponseChecksumValidation.WHEN_SUPPORTED, config.ResponseChecksumValidation);
        Assert.False(o.EffectiveUnsignedPayload);
    }

    [Fact]
    public void Options_CanTurnOffAutoBehaviour()
    {
        var o = Options(R2);
        o.UnsignedPayload = false;
        o.ChecksumWhenRequired = false;
        o.ConditionalWrites = false;
        Assert.Equal(RequestChecksumCalculation.WHEN_SUPPORTED, S3FileStorage.CreateClientConfig(o).RequestChecksumCalculation);

        using var storage = Storage(o);
        Assert.False(storage.ConditionalWritesEnabled);
        var request = storage.CreatePutRequest("a.pdf", new MemoryStream([1]), conditional: storage.ConditionalWritesEnabled);
        Assert.NotEqual(true, request.DisablePayloadSigning);
        Assert.Null(request.IfNoneMatch);
    }
}
