using System.Reflection;
using System.Text.RegularExpressions;
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
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCase();
}
