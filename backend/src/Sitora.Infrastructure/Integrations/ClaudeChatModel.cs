using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Настройки Claude (раздел Claude; в разработке — user secrets Claude:ApiKey, на сервере — переменная Claude__ApiKey).
public class ClaudeOptions
{
    public const string SectionName = "Claude";

    public string? ApiKey { get; set; }

    // Быстрая и недорогая модель для чата на сайте.
    public string Model { get; set; } = "claude-haiku-5-5";

    public int MaxTokens { get; set; } = 1000;
}

// Запрос к Claude через официальный SDK. Ключ берётся из настроек, в код и в браузер не попадает.
public sealed class ClaudeChatModel(IOptions<ClaudeOptions> options, ILogger<ClaudeChatModel> logger) : IChatModel
{
    private const string Unavailable = "AI помощник временно недоступен, попробуйте позже";
    private AnthropicClient? _client;

    public async Task<string> CompleteAsync(string system, IReadOnlyList<AiChatMessage> messages, CancellationToken ct = default)
    {
        var o = options.Value;
        _client ??= new AnthropicClient { ApiKey = o.ApiKey };

        Message response;
        try
        {
            response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = o.Model,
                MaxTokens = o.MaxTokens,
                System = system,
                // Чат: короткие ответы важнее глубины рассуждений.
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = messages
                    .Select(m => m.Role == "assistant"
                        ? (MessageParam)new AssistantMessageParam { Content = m.Content }
                        : new UserMessageParam { Content = m.Content })
                    .ToList(),
            }, ct);
        }
        catch (AnthropicRateLimitException e)
        {
            logger.LogWarning(e, "Claude: превышен лимит запросов");
            throw new ServiceUnavailableException(Unavailable);
        }
        catch (AnthropicApiException e)
        {
            logger.LogError(e, "Claude не ответил");
            throw new ServiceUnavailableException(Unavailable);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Нет связи с Claude");
            throw new ServiceUnavailableException(Unavailable);
        }

        if (response.StopReason == "refusal")
            return "Не могу помочь с этим вопросом. Попробуйте сформулировать его иначе или обратитесь к менеджеру.";

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
        return text.Length > 0 ? text : "Не получилось сформулировать ответ. Попробуйте задать вопрос иначе.";
    }
}
