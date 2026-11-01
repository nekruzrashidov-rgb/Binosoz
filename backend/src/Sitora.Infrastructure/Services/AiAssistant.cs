using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Помощник: покупателю — вопросы о квартире, сделке и сроках; сотрудникам — документы, CRM и письма.
// История диалога приходит из браузера и в базе не хранится.
public class AiAssistant(IChatModel model, ICurrentUser currentUser, IOptions<ClaudeOptions> options, AppDbContext db, DealAccess access, TimeProvider time) : IAiAssistant
{
    public const string NotConfigured = "AI помощник не настроен";
    private const int MaxMessageLength = 2000;
    private const int MaxHistory = 20;
    private const int MaxCustomerDeals = 3;

    // Общее для обоих промптов: страна, валюта и язык. Без этого модель пишет «руб.» и ссылается на российские законы.
    private const string Region =
        "Застройщик работает в Таджикистане (Душанбе). Цены и платежи — в сомони (TJS) и долларах США (USD), " +
        "рубли не упоминай. Если речь о законах, имей в виду законодательство Республики Таджикистан, а не России. " +
        "Отвечай на языке вопроса: на русском или на таджикском.";

    public const string CustomerPrompt =
        "Ты помощник жилого комплекса Sitora Residence. " + Region + " " +
        "Помогай покупателям с вопросами о квартирах, документах, рассрочке и сроках сдачи. " +
        "Если выше есть данные сделок покупателя, отвечай по ним: суммы, даты, просрочки. " +
        "Чего в данных нет, не придумывай, а предложи уточнить у менеджера. " +
        "Не давай юридических советов. Будь вежливым и кратким.";

    public const string StaffPrompt =
        "Ты помощник для сотрудников застройщика. " + Region + " " +
        "Помогай создавать документы (договоры, акты, счета), анализировать данные CRM, " +
        "составлять шаблоны писем клиентам. Если выше есть данные сделки, опирайся на них и не придумывай недостающие цифры. " +
        "Будь профессиональным и точным.";

    public Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct = default) =>
        ChatWithContextAsync(request, null, ct);

    // Сотруднику с открытой сделкой в начало промпта добавляются её данные, чтобы не пересказывать их в каждом вопросе.
    // Покупателю — данные его действующих сделок: dealId от него не принимается, чужие сделки в контекст не попадают.
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
            system = await CustomerContextAsync(ct) is { } mine ? mine + "\n\n" + CustomerPrompt : CustomerPrompt;
        else if (dealId is { } id)
        {
            if (!await access.CanViewAsync(id, ct))
                throw new NotFoundException("Сделка не найдена");
            system = "Текущая сделка: " + await DealContextAsync(id, ct) + "\n\n" + StaffPrompt;
        }
        else
            system = StaffPrompt;

        var messages = Conversation(request.History, message);
        var reply = await model.CompleteAsync(system, messages, ct);
        return new AiChatResponse(reply);
    }

    private async Task<string?> CustomerContextAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return null;
        var ids = await db.Deals.AsNoTracking()
            .Where(d => d.Customer.UserId == userId && d.Stage != DealStage.Cancelled)
            .OrderByDescending(d => d.CreatedAt).Select(d => d.Id).Take(MaxCustomerDeals)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return null;

        var lines = new List<string>();
        foreach (var id in ids)
            lines.Add("- " + await DealContextAsync(id, ct));
        return "Сделки покупателя:\n" + string.Join("\n", lines);
    }

    // Сделка одной строкой: квартира, дом и стройка, цена, оплачено и остаток, просрочка, следующий платёж.
    // Права проверяет вызывающий: сотрудника — DealAccess, покупателя — выборка по его учётной записи.
    private async Task<string> DealContextAsync(Guid dealId, CancellationToken ct)
    {
        var deal = await db.Deals.AsNoTracking().Where(d => d.Id == dealId)
            .Select(d => new
            {
                Buyer = d.Customer.FullName,
                Apartment = d.Apartment.Number,
                Floor = d.Apartment.Floor.Number,
                d.Apartment.Floor.BuildingId,
                Building = d.Apartment.Floor.Building.NameRu,
                d.Apartment.Floor.Building.CompletionYear,
                Price = d.Price - d.Discount,
                d.Currency,
                d.Stage
            })
            .SingleAsync(ct);
        var items = await db.PaymentScheduleItems.AsNoTracking().Where(i => i.Plan.DealId == dealId)
            .OrderBy(i => i.DueDate)
            .Select(i => new { i.DueDate, i.AmountDue, i.AmountPaid })
            .ToListAsync(ct);
        var stages = await db.ConstructionStages.AsNoTracking().Where(s => s.BuildingId == deal.BuildingId)
            .Select(s => s.Percent).ToListAsync(ct);

        string Money(decimal amount) => amount.ToString("N2", Russian) + " " + deal.Currency;
        string Date(DateOnly date) => date.ToString("dd.MM.yyyy", Russian);

        var context = $"покупатель {deal.Buyer}, квартира {deal.Apartment}, этаж {deal.Floor}, {deal.Building}";
        if (deal.CompletionYear is { } year)
            context += $", плановая сдача дома — {year} год";
        if (stages.Count > 0)
            context += $", готовность стройки {Math.Round(stages.Average())}%";
        context += $". Цена {Money(deal.Price)}, этап сделки: {StageText(deal.Stage)}.";
        if (items.Count == 0)
            return context + " Графика платежей нет.";

        var today = DushanbeTime.Today(time.GetUtcNow().UtcDateTime);
        var paid = items.Sum(i => i.AmountPaid);
        var open = items.Where(i => i.AmountPaid < i.AmountDue).ToList();
        var overdue = open.Where(i => i.DueDate < today).ToList();
        var next = open.FirstOrDefault(i => i.DueDate >= today);

        context += $" Оплачено {Money(paid)}, остаток {Money(items.Sum(i => i.AmountDue) - paid)}; " +
                   $"закрыто платежей по графику: {items.Count - open.Count} из {items.Count}.";
        context += overdue.Count == 0
            ? " Просрочек нет."
            : $" Просрочено платежей: {overdue.Count} на {Money(overdue.Sum(i => i.AmountDue - i.AmountPaid))}, самый ранний — с {Date(overdue[0].DueDate)}.";
        context += next is null
            ? " Будущих платежей нет."
            : $" Следующий платёж по графику: {Date(next.DueDate)}, {Money(next.AmountDue - next.AmountPaid)}.";
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
