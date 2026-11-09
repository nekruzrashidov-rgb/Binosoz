using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.Interfaces;

// Инструменты CRM: импорт клиентов из Excel, шаблоны сообщений, аналитика продаж, карточка проекта.
public interface ICrmToolsService
{
    Task<ClientImportResult> ImportClientsAsync(Stream xlsx, bool dryRun, CancellationToken ct = default);

    Task<IReadOnlyList<MessageTemplateResponse>> TemplatesAsync(CancellationToken ct = default);
    Task<MessageTemplateResponse> CreateTemplateAsync(MessageTemplateRequest request, CancellationToken ct = default);
    Task<MessageTemplateResponse> UpdateTemplateAsync(Guid id, MessageTemplateRequest request, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);

    Task<SalesAnalyticsResponse> SalesAnalyticsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProjectCardResponse>> ProjectCardsAsync(CancellationToken ct = default);
}
