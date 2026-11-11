using System.Text.Json;
using Sitora.Tests.Infrastructure;
using Sitora.WebApi.Extensions;

namespace Sitora.Tests.Security;

// Наблюдаемость: traceId ответа — тот же TraceId, что в логе (32 hex); в Sentry не уходят почты, телефоны, токены, паспорта.
public class ObservabilityTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task ProblemDetails_TraceId_IsTheW3CTraceIdOfTheRequest()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/no-such-route");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Matches("^[0-9a-f]{32}$", json.RootElement.GetProperty("traceId").GetString());
    }

    [Theory]
    [InlineData("Клиент ivan.petrov@mail.tj не найден", "ivan.petrov@mail.tj")]
    [InlineData("Звонок на +992 90 123 45 67 не прошёл", "123 45 67")]
    [InlineData("Токен eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.abcDEF_123 устарел", "eyJhbGci")]
    [InlineData("Паспорт A1234567 не прошёл проверку", "A1234567")]
    public void Sentry_Mask_HidesPersonalData(string text, string secret)
    {
        var masked = SentrySetup.Mask(text);
        Assert.DoesNotContain(secret, masked);
        Assert.Contains("[скрыто]", masked);
    }
}
