using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sitora.Domain.DTOs.Auth;
using Sitora.Tests.Infrastructure;
using Sitora.WebApi.Errors;

namespace Sitora.Tests.Security;

// Каталог ошибок полный: у каждой записи есть машинный code (snake_case) и таджикский текст, не равный русскому.
public partial class ErrorCatalogTests
{
    [Fact]
    public void EveryEntry_HasCode_AndTajikText()
    {
        var exact = (Dictionary<string, ErrorCatalog.Entry>)typeof(ErrorCatalog)
            .GetField("Exact", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var prefixes = (List<(string Prefix, ErrorCatalog.Entry Entry)>)typeof(ErrorCatalog)
            .GetField("Prefixes", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        var all = exact.Select(e => (Ru: e.Key, e.Value)).Concat(prefixes.Select(p => (Ru: p.Prefix, Value: p.Entry))).ToList();
        Assert.True(all.Count > 200, $"В каталоге всего {all.Count} записей");

        var bad = all.Where(e => string.IsNullOrWhiteSpace(e.Value.Tg) || e.Value.Tg.Trim() == e.Ru.Trim() || !SnakeCase().IsMatch(e.Value.Code))
            .Select(e => $"{e.Value.Code}: {e.Ru}").ToList();
        Assert.True(bad.Count == 0, "Записи без кода или перевода:\n" + string.Join("\n", bad));

        // R-09: английский текст есть у каждой записи и написан латиницей (не скопирован русский).
        var noEnglish = all.Where(e => string.IsNullOrWhiteSpace(e.Value.En) || Cyrillic().IsMatch(e.Value.En))
            .Select(e => $"{e.Value.Code}: {e.Ru}").ToList();
        Assert.True(noEnglish.Count == 0, "Записи без английского текста:\n" + string.Join("\n", noEnglish));
    }

    [Theory]
    [InlineData("Неверная почта или пароль", ErrorCatalog.Language.En, "Wrong e-mail or password")]
    [InlineData("Неверная почта или пароль", ErrorCatalog.Language.Ru, "Неверная почта или пароль")]
    [InlineData("Неизвестная роль: Boss", ErrorCatalog.Language.En, "Unknown role: Boss")]
    [InlineData("Текста нет в каталоге", ErrorCatalog.Language.En, "Текста нет в каталоге")]
    public void Translate_English_WithPrefixTail_AndRussianFallback(string ru, ErrorCatalog.Language language, string expected) =>
        Assert.Equal(expected, ErrorCatalog.Translate(ru, language));

    [GeneratedRegex("[А-Яа-яЁё]")]
    private static partial Regex Cyrillic();

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCase();
}

// R-09: ошибки API на языке сайта — Accept-Language: en даёт английский текст, tg — таджикский, без заголовка — русский.
public class ErrorLanguageTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Theory]
    [InlineData("en", "Wrong e-mail or password")]
    [InlineData("en-US,en;q=0.9", "Wrong e-mail or password")]
    [InlineData("tg", "Почтаи электронӣ ё парол нодуруст аст")]
    [InlineData(null, "Неверная почта или пароль")]
    public async Task Login_WrongPassword_InTheLanguageOfTheRequest(string? language, string expected)
    {
        var client = factory.CreateClient();
        if (language is not null)
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.AdminEmail, "Wrong-Password-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expected, problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal("invalid_credentials", problem.RootElement.GetProperty("code").GetString());
    }
}
