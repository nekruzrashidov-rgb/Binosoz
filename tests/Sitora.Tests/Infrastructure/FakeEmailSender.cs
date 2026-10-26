using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Sitora.Domain.Interfaces;

namespace Sitora.Tests.Infrastructure;

// Вместо отправки запоминает письма: последний код входа и последний токен приглашения на каждую почту.
public partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, List<EmailMessage>> _messages = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _messages.AddOrUpdate(message.To, _ => [message], (_, list) =>
        {
            lock (list)
                list.Add(message);
            return list;
        });
        return Task.CompletedTask;
    }

    public int SentCount(string email) => _messages.TryGetValue(email, out var list) ? list.Count : 0;

    public string? LastCode(string email) => Last(email, CodePattern());

    public string? LastInviteToken(string email) => Last(email, InvitePattern(), group: 1);

    private string? Last(string email, Regex pattern, int group = 0)
    {
        if (!_messages.TryGetValue(email, out var list))
            return null;
        lock (list)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var match = pattern.Match(list[i].Text);
                if (match.Success)
                    return match.Groups[group].Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"(?<![0-9A-F])\d{6}(?![0-9A-F])")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"#invite=([0-9A-F]{64})")]
    private static partial Regex InvitePattern();
}
