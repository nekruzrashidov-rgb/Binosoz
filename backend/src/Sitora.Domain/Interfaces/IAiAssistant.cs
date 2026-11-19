using Sitora.Domain.DTOs.Ai;

namespace Sitora.Domain.Interfaces;

// AI-помощник в чате сайта: промпт выбирается по роли текущего пользователя.
public interface IAiAssistant
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct = default);

    // Сотруднику — с данными сделки в начале промпта (покупатель, квартира, цена, этап, платежи). Чужая сделка — 404.
    Task<AiChatResponse> ChatWithContextAsync(AiChatRequest request, Guid? dealId, CancellationToken ct = default);
}

// Помощник гостя на публичном сайте застройщика: только вопросы о покупке квартиры в этом ЖК, вход не нужен.
public interface IPublicAiAssistant
{
    // Включён ли помощник (задан ключ Claude). Неизвестная или приостановленная организация — 404.
    Task<AiStatusResponse> StatusAsync(string orgSlug, CancellationToken ct = default);

    // Вопрос ≤ 500 символов, история — последние 10 реплик user/assistant. Суточный лимит организации — 429.
    Task<AiChatResponse> ChatAsync(string orgSlug, AiChatRequest request, CancellationToken ct = default);

    // Контакты организации для ответа 429 (только заполненные); null — организации нет.
    Task<Dictionary<string, string>?> ContactsAsync(string orgSlug, CancellationToken ct = default);
}

// Один запрос к языковой модели: системный промпт и диалог, где реплики чередуются (user, assistant, …, user).
// maxTokens — свой предел длины ответа (публичный чат короче); null — из настроек.
public interface IChatModel
{
    Task<string> CompleteAsync(string system, IReadOnlyList<AiChatMessage> messages, CancellationToken ct = default, int? maxTokens = null);
}
