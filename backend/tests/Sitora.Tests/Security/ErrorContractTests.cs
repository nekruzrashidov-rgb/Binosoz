using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Контракт ошибок: в title, detail и текстах errors нет латиницы (кроме имён полей — это ключи), у каждой ошибки
// есть code и traceId; битый JSON — одна ошибка без текста парсера; Accept-Language: tg — таджикский текст.
public partial class ErrorContractTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    public static TheoryData<string, string, string?, string, HttpStatusCode, string> Cases => new()
    {
        // метод, адрес, тело (null — без тела), тип содержимого, ожидаемый код ответа, ожидаемый code
        { "POST", "/api/v1/auth/email/send", "{}", "application/json", HttpStatusCode.BadRequest, "validation_failed" },
        { "POST", "/api/v1/leads", "{}", "application/json", HttpStatusCode.BadRequest, "validation_failed" },
        { "POST", "/api/v1/auth/login", "{}", "application/json", HttpStatusCode.BadRequest, "validation_failed" },
        { "POST", "/api/v1/leads", "{bad json", "application/json", HttpStatusCode.BadRequest, "malformed_request" },
        { "POST", "/api/v1/leads", null, "application/json", HttpStatusCode.BadRequest, "malformed_request" },
        { "POST", "/api/v1/leads", "name=1", "text/plain", HttpStatusCode.UnsupportedMediaType, "unsupported_media_type" },
        { "GET", "/api/v1/no-such-route", null, "", HttpStatusCode.NotFound, "route_not_found" },
        { "GET", "/api/v1/apartments/abc", null, "", HttpStatusCode.NotFound, "route_not_found" },
        { "GET", $"/api/v1/apartments/{Guid.Empty}", null, "", HttpStatusCode.NotFound, "apartment_not_found" },
        { "GET", "/api/v1/deals", null, "", HttpStatusCode.Unauthorized, "unauthorized" },
        { "DELETE", "/api/v1/buildings?organization=nekruz", null, "", HttpStatusCode.MethodNotAllowed, "method_not_allowed" },
        { "GET", "/api/v1/buildings", null, "", HttpStatusCode.BadRequest, "organization_required" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Errors_AreRussian_WithCodeAndTraceId(string method, string url, string? body, string contentType, HttpStatusCode status, string code)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, url, body, contentType));

        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(code == root.GetProperty("code").GetString(), root.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        foreach (var text in Texts(root))
            Assert.False(LatinRegex().IsMatch(text.Replace("JSON", "", StringComparison.Ordinal)), $"Латиница в ответе: «{text}»");
        Assert.DoesNotContain("BytePositionInLine", root.GetRawText());
    }

    [Fact]
    public async Task Leads_EmptyBody_HasOneMessagePerField()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/leads", new { });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");

        Assert.Equal(["Укажите имя"], errors.GetProperty("name").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, errors.GetProperty("phone").GetArrayLength());
        Assert.Equal("Проверьте введённые данные", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ServerError_HasNoInternals()
    {
        var response = await factory.CreateClient().SendAsync(Request("GET", "/api/v1/apartments/abc", null, ""));
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", text);
    }

    [Theory]
    [InlineData("GET", $"/api/v1/apartments/00000000-0000-0000-0000-000000000000", null, "Манзил ёфт нашуд")]
    [InlineData("GET", "/api/v1/deals", null, "Ворид шудан лозим аст")]
    [InlineData("POST", "/api/v1/leads", "{}", "Маълумоти воридшударо санҷед")]
    [InlineData("GET", "/api/v1/no-such-route", null, "Чунин суроға вуҷуд надорад")]
    public async Task Tajik_AcceptLanguage_GivesTajikText(string method, string url, string? body, string expected)
    {
        var request = Request(method, url, body, "application/json");
        request.Headers.AcceptLanguage.ParseAdd("tg");
        var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(expected, Regex.Unescape(text));
    }

    [Fact]
    public async Task Tajik_ValidationMessages_AreTranslated()
    {
        var request = Request("POST", "/api/v1/leads", "{}", "application/json");
        request.Headers.AcceptLanguage.ParseAdd("tg");
        using var json = JsonDocument.Parse(await (await factory.CreateClient().SendAsync(request)).Content.ReadAsStringAsync());
        Assert.Equal("Номро нишон диҳед", json.RootElement.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    private static HttpRequestMessage Request(string method, string url, string? body, string contentType)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        else if (method == "POST")
            request.Content = new ByteArrayContent([]) { Headers = { ContentType = new("application/json") } };
        return request;
    }

    // Тексты для человека: title, detail и сообщения в errors (ключи errors — имена полей, они латиницей).
    private static IEnumerable<string> Texts(JsonElement root)
    {
        foreach (var name in new[] { "title", "detail" })
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                yield return value.GetString()!;
        if (root.TryGetProperty("errors", out var errors))
            foreach (var field in errors.EnumerateObject())
                foreach (var message in field.Value.EnumerateArray())
                    yield return message.GetString()!;
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinRegex();
}
