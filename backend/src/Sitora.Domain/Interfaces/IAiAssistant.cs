using Sitora.Domain.DTOs.Ai;

namespace Sitora.Domain.Interfaces;

// AI-помощник в чате сайта: промпт выбирается по роли текущего пользователя.
public interface IAiAssistant
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct = default);

    // Сотруднику — с данными сделки в начале промпта (покупатель, квартира, цена, этап, платежи). Чужая сделка — 404.
    Task<AiChatResponse> ChatWithContextAsync(AiChatRequest request, Guid? dealId, CancellationToken ct = default);
}

// Один запрос к языковой модели: системный промпт и диалог, где реплики чередуются (user, assistant, …, user).
public interface IChatModel
{
    Task<string> CompleteAsync(string system, IReadOnlyList<AiChatMessage> messages, CancellationToken ct = default);
}
