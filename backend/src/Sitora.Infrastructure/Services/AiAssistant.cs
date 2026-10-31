using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Помощник: покупателю — вопросы о квартире, сделке и сроках; сотрудникам — документы, CRM и письма.
// История диалога приходит из браузера и в базе не хранится.
public class AiAssistant(IChatModel model, ICurrentUser currentUser, IOptions<ClaudeOptions> options, AppDbContext db, DealAccess access) : IAiAssistant
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

    public Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct = default) =>
        ChatWithContextAsync(request, null, ct);

    // Сотруднику с открытой сделкой в начало промпта добавляются её данные, чтобы не пересказывать их в каждом вопросе.
    // Покупатель всегда получает свой промпт без контекста: свою сделку он и так знает, чужие ему недоступны.
    public async Task<AiChatResponse> ChatWithContextAsync(AiChatRequest request, Guid? dealId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new ServiceUnavailableException(NotConfigured);

        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message))
            throw new BusinessRuleException("Напишите вопрос");
        if (message.Length > MaxMessageLength)
            throw new BusinessRuleException($"Вопрос — не длиннее {MaxMessageLength} символов");

        // Покупатель получает свой промпт; все роли застройщика и платформы — промпт сотрудника.
        string system;
        if (currentUser.IsInRole(RoleNames.Customer))
            system = CustomerPrompt;
        else
            system = dealId is { } id ? await DealContextAsync(id, ct) + "\n\n" + StaffPrompt : StaffPrompt;

        var messages = Conversation(request.History, message);
        var reply = await model.CompleteAsync(system, messages, ct);
        return new AiChatResponse(reply);
    }

    private async Task<string> DealContextAsync(Guid dealId, CancellationToken ct)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");

        var deal = await db.Deals.AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new
            {
                Buyer = d.Customer.FullName,
                Apartment = d.Apartment.Number,
                Floor = d.Apartment.Floor.Number,
                Building = d.Apartment.Floor.Building.NameRu,
                Price = d.Price - d.Discount,
                d.Currency,
                d.Stage
            })
            .SingleAsync(ct);
        var items = await db.PaymentScheduleItems.AsNoTracking().Where(i => i.Plan.DealId == dealId)
            .Select(i => new { i.DueDate, i.AmountDue, i.AmountPaid })
            .ToListAsync(ct);

        var paid = items.Count(i => i.AmountPaid >= i.AmountDue);
        var next = items.Where(i => i.AmountPaid < i.AmountDue).OrderBy(i => i.DueDate).Select(i => (DateOnly?)i.DueDate).FirstOrDefault();
        var price = deal.Price.ToString("N2", Russian);
        var context = $"Текущая сделка: {deal.Buyer}, квартира {deal.Apartment} этаж {deal.Floor}, {deal.Building}, " +
                      $"цена {price} {deal.Currency}, статус {StageText(deal.Stage)}.";
        context += items.Count == 0
            ? " Графика платежей нет."
            : $" Платежей: {paid}/{items.Count}, следующий {(next is { } due ? due.ToString("dd.MM.yyyy", Russian) : "—")}.";
        return context;
    }

    private static readonly System.Globalization.CultureInfo Russian = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");

    private static string StageText(DealStage stage) => stage switch
    {
        DealStage.Lead => "заявка",
        DealStage.Showing => "показ",
        DealStage.Booking => "бронь",
        DealStage.Contract => "договор",
        DealStage.Payment => "оплата",
        DealStage.Completed => "завершена",
        _ => "отменена"
    };

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
