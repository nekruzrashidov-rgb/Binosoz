using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Sitora.Domain.Interfaces;

namespace Sitora.Tests.Infrastructure;

// Вместо отправки запоминает последний код на каждый номер.
public partial class FakeSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _lastCodes = new();
    private readonly ConcurrentDictionary<string, int> _counts = new();

    public Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        _lastCodes[phone] = CodePattern().Match(text).Value;
        _counts.AddOrUpdate(phone, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }

    public string? LastCode(string phone) => _lastCodes.GetValueOrDefault(phone);

    public int SentCount(string phone) => _counts.GetValueOrDefault(phone);

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex CodePattern();
}
