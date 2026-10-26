using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Помощник: покупателю — вопросы о квартире, сделке и сроках; сотрудникам — документы, CRM и письма.
// История диалога приходит из браузера и в базе не хранится.
public class AiAssistant(IChatModel model, ICurrentUser currentUser, IOptions<ClaudeOptions> options) : IAiAssistant
{
    public const string NotConfigured = "AI помощник не настроен";
    private const int MaxMessageLength = 2000;
    private const int MaxHistory = 20;

    public const string CustomerPrompt =
        "Ты помощник жилого комплекса Sitora Residence. Отвечай только на русском языке. " +
        "Помогай покупателям с вопросами о квартирах, документах, рассрочке и сроках сдачи. " +
        "Не давай юридических советов. Будь вежливым и кратким.";

    public const string StaffPrompt =
        "Ты помощник для сотрудников застройщика. Отвечай только на русском языке. " +
        "Помогай создавать документы (договоры, акты, счета), анализировать данные CRM, " +
        "составлять шаблоны писем клиентам. Будь профессиональным и точным.";

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new ServiceUnavailableException(NotConfigured);

        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message))
            throw new BusinessRuleException("Напишите вопрос");
        if (message.Length > MaxMessageLength)
            throw new BusinessRuleException($"Вопрос — не длиннее {MaxMessageLength} символов");

        // Покупатель получает свой промпт; все роли застройщика и платформы — промпт сотрудника.
        var system = currentUser.IsInRole(RoleNames.Customer) ? CustomerPrompt : StaffPrompt;

        var messages = Conversation(request.History, message);
        var reply = await model.CompleteAsync(system, messages, ct);
        return new AiChatResponse(reply);
    }

    // Модель ждёт чередование реплик, начиная с пользователя: лишнее отбрасывается, длинная история обрезается с начала.
    private static List<AiChatMessage> Conversation(IReadOnlyList<AiChatMessage>? history, string message)
    {
        var result = new List<AiChatMessage>();
        foreach (var item in (history ?? []).TakeLast(MaxHistory))
        {
            var role = item.Role?.Trim().ToLowerInvariant();
            var content = item.Content?.Trim();
            if (role is not ("user" or "assistant") || string.IsNullOrEmpty(content))
                continue;
            if (content.Length > MaxMessageLength * 2)
                content = content[..(MaxMessageLength * 2)];
            if (result.Count == 0 ? role != "user" : result[^1].Role == role)
                continue;
            result.Add(new AiChatMessage(role, content));
        }

        if (result.Count > 0 && result[^1].Role == "user")
            result.RemoveAt(result.Count - 1);
        result.Add(new AiChatMessage("user", message));
        return result;
    }
}
