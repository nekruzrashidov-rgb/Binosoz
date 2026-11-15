using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Контракт фронт ↔ бэк: каждый путь, который вызывает frontend/js/zaprosho-ba-backend.js (request('GET', '/…') и all('/…')),
// есть среди маршрутов API с тем же методом. Выражения в пути ('/deals/' + id + '/schedule') — параметр маршрута.
public class FrontendContractTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static string ClientFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "frontend", "js", "zaprosho-ba-backend.js");
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("frontend/js/zaprosho-ba-backend.js не найден");
    }

    // Вызовы клиента: метод и путь в виде сегментов ("{}" — подставленное значение).
    public static List<(string Method, string Path)> ClientCalls(string source)
    {
        var calls = new List<(string, string)>();
        foreach (Match m in Regex.Matches(source, @"\b(?:request\(\s*'(?<m>GET|POST|PUT|PATCH|DELETE)'\s*,|all\()\s*(?=')"))
        {
            var method = m.Groups["m"].Success ? m.Groups["m"].Value : "GET";
            var expr = ReadExpression(source, m.Index + m.Length);
            var path = ToPath(expr);
            if (path is not null)
                calls.Add((method, path));
        }
        return calls;
    }

    // Выражение аргумента до запятой или скобки на нулевой глубине: '/a/' + id + '/b'.
    private static string ReadExpression(string s, int start)
    {
        var depth = 0;
        var inString = false;
        for (var i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (inString) { if (c == '\\') i++; else if (c == '\'') inString = false; continue; }
            if (c == '\'') { inString = true; continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') { if (depth == 0) return s[start..i]; depth--; }
            else if (c == ',' && depth == 0) return s[start..i];
        }
        return s[start..];
    }

    private static string? ToPath(string expr)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var part in SplitPlus(expr))
        {
            var p = part.Trim();
            if (p.Length >= 2 && p[0] == '\'' && p[^1] == '\'')
                sb.Append(p[1..^1]);
            else
                sb.Append("{}");
        }
        var path = sb.ToString();
        var q = path.IndexOf('?');
        if (q >= 0)
            path = path[..q];
        return path.StartsWith('/') ? "/" + string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries)) : null;
    }

    private static IEnumerable<string> SplitPlus(string expr)
    {
        var depth = 0; var inString = false; var last = 0;
        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (inString) { if (c == '\\') i++; else if (c == '\'') inString = false; continue; }
            if (c == '\'') inString = true;
            else if (c is '(' or '[') depth++;
            else if (c is ')' or ']') depth--;
            else if (c == '+' && depth == 0) { yield return expr[last..i]; last = i + 1; }
        }
        yield return expr[last..];
    }

    private static bool Matches(string[] client, string[] route)
    {
        if (client.Length != route.Length)
            return false;
        for (var i = 0; i < client.Length; i++)
        {
            var routeParam = route[i].StartsWith('{');
            if (client[i].Contains("{}"))
            {
                // The client puts a value here: the route has a parameter (maybe with a suffix like {id}.pdf).
                if (!route[i].Contains('{'))
                    return false;
            }
            else if (!routeParam && !string.Equals(client[i], route[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    [Fact]
    public void EveryClientCall_HasAnApiRoute_WithTheSameMethod()
    {
        var calls = ClientCalls(File.ReadAllText(ClientFile()));
        Assert.True(calls.Count > 80, "Разобрано слишком мало вызовов: " + calls.Count);

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (Methods: e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? [],
                Segments: (e.RoutePattern.RawText ?? "").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)))
            .Where(r => r.Segments.Length > 2 && r.Segments[0] == "api" && r.Segments[1] == "v1")
            .Select(r => (r.Methods, Segments: r.Segments[2..]))
            .ToList();

        var missing = calls.Distinct()
            .Where(c => !routes.Any(r => (r.Methods.Count == 0 || r.Methods.Contains(c.Method)) && Matches(c.Path.Trim('/').Split('/'), r.Segments)))
            .Select(c => c.Method + " " + c.Path)
            .ToList();
        Assert.True(missing.Count == 0, "Нет маршрута в API для вызовов фронтенда:\n" + string.Join('\n', missing));
    }
}
