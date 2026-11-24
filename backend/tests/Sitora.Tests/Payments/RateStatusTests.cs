using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Jobs;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

// C-09: загрузка курса НБТ — причина неудачи (без сети: подменный HTTP), статус «последняя загрузка» для CRM → «Курс»
// и догон при старте, если курс старше суток.
public class NbtRateProviderTests
{
    private const string Xml = "<?xml version=\"1.0\" encoding=\"windows-1251\" ?><ValCurs Date=\"2026-11-24\">" +
                               "<Valute ID=\"840\"><CharCode>USD</CharCode><Nominal>1</Nominal><Value>9.2330</Value></Valute></ValCurs>";

    [Fact]
    public async Task Xml_GivesTheRate()
    {
        var result = await Provider(_ => Response(HttpStatusCode.OK, Xml)).FetchUsdRateAsync(new DateOnly(2026, 11, 24));
        Assert.Equal((9.2330m, (string?)null), (result.Rate, result.Error));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", "503")]
    [InlineData(HttpStatusCode.OK, "<!DOCTYPE html><html><body>Bitrix</body></html>", "не XML")]
    [InlineData(HttpStatusCode.OK, "<?xml version=\"1.0\"?><ValCurs Date=\"2026-11-22\"></ValCurs>", "нет курса USD")]
    public async Task BadAnswer_HasAReason(HttpStatusCode status, string body, string reason)
    {
        var result = await Provider(_ => Response(status, body)).FetchUsdRateAsync(new DateOnly(2026, 11, 24));
        Assert.Null(result.Rate);
        Assert.Contains(reason, result.Error);
    }

    [Fact]
    public async Task Timeout_AndNetworkError_HaveAReason()
    {
        var timeout = await Provider(_ => throw new TaskCanceledException("timeout")).FetchUsdRateAsync(new DateOnly(2026, 11, 24));
        Assert.Contains("таймаут", timeout.Error);
        var network = await Provider(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused"))
            .FetchUsdRateAsync(new DateOnly(2026, 11, 24));
        Assert.Contains("нет соединения", network.Error);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };

    private static NbtRateProvider Provider(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new Factory(new Handler(answer)), new ConfigurationBuilder().Build(), NullLogger<NbtRateProvider>.Instance);

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

public class RateStatusTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Status_ShowsTheLastAttempt_ErrorThenOk_AndCatchUpOnlyWhenStale()
    {
        var accountant = await factory.CreateAccountantClientAsync();
        var empty = (await accountant.GetFromJsonAsync<RateUpdateStatus>("/api/v1/rates/status", TestJson.Options))!;
        Assert.Null(empty.Ok);
        await factory.WithServiceAsync<IExchangeRateService>(async s => Assert.True(await s.NeedsCatchUpAsync()));

        // Сайт НБТ не ответил: попытка и причина видны, догон не повторяется чаще раза в час.
        factory.Rates.Usd = null;
        await factory.WithServiceAsync<UpdateRatesJob>(job => job.RunAsync(CancellationToken.None));
        var failed = (await accountant.GetFromJsonAsync<RateUpdateStatus>("/api/v1/rates/status", TestJson.Options))!;
        Assert.False(failed.Ok);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
        Assert.NotNull(failed.LastAttemptAt);
        Assert.Null(failed.LastSuccessAt);
        await factory.WithServiceAsync<IExchangeRateService>(async s => Assert.False(await s.NeedsCatchUpAsync()));

        // Курс получен: ok, дата курса НБТ — сегодня, догон не нужен.
        factory.Rates.Usd = 9.25m;
        await factory.WithServiceAsync<UpdateRatesJob>(job => job.RunAsync(CancellationToken.None));
        var ok = (await accountant.GetFromJsonAsync<RateUpdateStatus>("/api/v1/rates/status", TestJson.Options))!;
        Assert.True(ok.Ok);
        Assert.Null(ok.Error);
        Assert.Equal(TestScenarios.Today, ok.LastNbtDate);
        Assert.NotNull(ok.LastSuccessAt);
        await factory.WithServiceAsync<IExchangeRateService>(async s => Assert.False(await s.NeedsCatchUpAsync()));
        factory.Rates.Usd = null;

        // Менеджеру и гостю статус не нужен.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateManagerClientAsync()).GetAsync("/api/v1/rates/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/rates/status")).StatusCode);
    }
}
