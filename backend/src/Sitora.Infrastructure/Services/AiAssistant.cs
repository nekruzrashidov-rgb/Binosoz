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

// Помощник: покупателю — его квартира, платежи и дом; сотрудникам — CRM, правила и API; создателю платформы — организации и тарифы.
// История диалога приходит из браузера и в базе не хранится.
public class AiAssistant(IChatModel model, ICurrentUser currentUser, IOptions<ClaudeOptions> options, AppDbContext db, DealAccess access, TimeProvider time) : IAiAssistant
{
    public const string NotConfigured = "AI помощник не настроен";
    private const int MaxMessageLength = 2000;
    private const int MaxHistory = 20;
    private const int MaxCustomerDeals = 3;

    // Общее для всех промптов: валюта и законы. Без этого модель пишет «руб.» и ссылается на российские законы.
    private const string Region =
        "Prices and payments are in Tajik somoni (TJS) and US dollars (USD); never mention rubles. " +
        "When laws come up, refer to the legislation of the Republic of Tajikistan, not Russia.";

    // Покупатель: его квартира, платежи и дом; отвечает на языке вопроса (таджикский, русский, английский).
    public const string CustomerPrompt =
        "You are the personal assistant of \"Некруз Резиденс\" residential complex in Dushanbe, Tajikistan.\n" +
        "You are speaking with a BUYER. Answer in the same language the user writes in (Tajik, Russian, or English).\n" +
        "Be concise and warm. Answer questions about:\n" +
        "- Their apartment (number, floor, area, layout, ceiling height)\n" +
        "- Payment schedule (next payment date, amount, remaining balance)\n" +
        "- Deal status and next steps toward ownership\n" +
        "- Building infrastructure (2 Otis elevators, underground parking B1, 24/7 security, children's playground, gym)\n" +
        "- Construction progress and delivery timeline\n" +
        "- Contact info: +992 98 363 70 70, Telegram @nekru01, office hours Mon-Fri 9-19, Sat 10-17\n" +
        "- General questions about buying real estate in Tajikistan\n" +
        "Never discuss other clients' data. Keep answers under 150 words unless breakdown is needed.\n" +
        "If context data is provided above, use it to give personalized answers. " +
        "What is not in the context, do not make up: suggest asking the manager.\n" + Region;

    // Сотрудники организации (владелец, админ объекта, менеджер, бухгалтер, контент): CRM, правила, API. {role} — роль.
    public const string StaffPromptTemplate =
        "You are a CRM assistant for \"Binosoz\" real estate platform. Working language: Russian.\n" +
        "You are speaking with a staff member (role: {role}).\n" +
        "Answer questions about:\n" +
        "- CRM workflows: leads → booking → deal → contract → payment → completed\n" +
        "- How to perform actions: create deal, add payment, upload document, build payment schedule\n" +
        "- Business rules: one active booking per apartment (48h), no payment deletion only reversal (ReversalOfId),\n" +
        "  audit log for all status/price/payment changes, JWT access 15min + refresh 30 days\n" +
        "- API endpoints: prefix /api/v1, Problem Details errors, pagination page/pageSize\n" +
        "- Hangfire jobs: ExpireBookingsJob, UpdateRatesJob, OverduePaymentJob, PaymentReminderJob\n" +
        "- Troubleshooting: 409 conflict on double booking, 402 on tariff limit, 422 on business rule violation\n" +
        "If deal data is provided above, rely on it and do not invent missing numbers.\n" +
        "Be precise and professional. Under 200 words unless technical detail needed.\n" + Region;

    // Создатель платформы (SuperAdmin, раньше PlatformOwner): организации, тарифы, пользователи, деплой.
    public const string PlatformPrompt =
        "You are a platform administration assistant for \"Binosoz\" SaaS. Working language: Russian.\n" +
        "You are speaking with the PLATFORM OWNER (superadmin).\n" +
        "Answer questions about:\n" +
        "- Managing organizations: create, suspend (DELETE .../suspend), activate (POST .../activate)\n" +
        "- Tariff plans: Free/Basic/Pro, limits on buildings and staff, assigning via POST /platform/organizations/{id}/tariff\n" +
        "- Dashboard metrics: organizations, deals, payments, top-5 orgs, monthly deals chart\n" +
        "- User management: block/unblock, password reset via POST /platform/users/{id}/reset-password\n" +
        "- Deployment: Docker Compose + Caddy reverse proxy, migrations with dotnet run --migrate\n" +
        "- Tech: .NET 10, PostgreSQL 18, EF Core, Hangfire, SignalR, QuestPDF, ClosedXML\n" +
        "Under 200 words. Be technical and precise.\n" + Region;

    public static string StaffPrompt(string role) => StaffPromptTemplate.Replace("{role}", role);

    // Роль сотрудника в промпте: первая из ролей организации, которая есть у пользователя.
    private static readonly string[] StaffRoles = [RoleNames.Owner, RoleNames.ObjectAdmin, RoleNames.Manager, RoleNames.Accountant, RoleNames.Content];

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

        // Покупатель — свой промпт и данные своих сделок; создатель платформы — промпт платформы (а «как владелец» —
        // промпт сотрудника); все остальные роли — промпт сотрудника с названием роли.
        string system;
        if (currentUser.IsInRole(RoleNames.Customer))
            system = await CustomerContextAsync(ct) is { } mine ? mine + "\n\n" + CustomerPrompt : CustomerPrompt;
        else
        {
            var prompt = currentUser.IsInRole(RoleNames.SuperAdmin) && currentUser.ImpersonatorId is null
                ? PlatformPrompt
                : StaffPrompt(StaffRoles.FirstOrDefault(currentUser.IsInRole) ?? RoleNames.Owner);
            if (dealId is { } id)
            {
                if (!await access.CanViewAsync(id, ct))
                    throw new NotFoundException("Сделка не найдена");
                system = "Текущая сделка: " + await DealContextAsync(id, ct) + "\n\n" + prompt;
            }
            else
                system = prompt;
        }

        var messages = Conversation(request.History, message);
        var reply = await model.CompleteAsync(system, messages, ct);
        return new AiChatResponse(reply);
    }

    // Блок BUYER CONTEXT на каждую действующую сделку покупателя (до трёх, новые первыми).
    // Оплачено и остаток — как в обзоре кабинета: сумма платежей (сторно с минусом) от цены со скидкой.
    private async Task<string?> CustomerContextAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return null;
        var deals = await db.Deals.AsNoTracking()
            .Where(d => d.Customer.UserId == userId && d.Stage != DealStage.Cancelled)
            .OrderByDescending(d => d.CreatedAt).Take(MaxCustomerDeals)
            .Select(d => new
            {
                d.Id,
                Name = d.Customer.FullName,
                d.Apartment.Number,
                Floor = d.Apartment.Floor.Number,
                d.Apartment.Rooms,
                d.Apartment.Area,
                d.Apartment.Floor.BuildingId,
                Building = d.Apartment.Floor.Building.NameRu,
                d.Apartment.Floor.Building.FloorsCount,
                d.Apartment.Floor.Building.CompletionYear,
                Apartments = d.Apartment.Floor.Building.Floors.SelectMany(f => f.Apartments).Count(),
                Price = d.Price - d.Discount,
                d.Currency,
                d.Stage,
                Paid = db.Payments.Where(p => p.DealId == d.Id).Sum(p => (decimal?)p.AmountInDealCurrency) ?? 0m
            })
            .ToListAsync(ct);
        if (deals.Count == 0)
            return null;

        var ids = deals.Select(d => d.Id).ToList();
        var items = await db.PaymentScheduleItems.AsNoTracking().Where(i => ids.Contains(i.Plan.DealId))
            .OrderBy(i => i.DueDate)
            .Select(i => new { i.Plan.DealId, i.DueDate, i.AmountDue, i.AmountPaid })
            .ToListAsync(ct);
        var buildingIds = deals.Select(d => d.BuildingId).Distinct().ToList();
        var readiness = (await db.ConstructionStages.AsNoTracking().Where(s => buildingIds.Contains(s.BuildingId))
                .Select(s => new { s.BuildingId, s.Percent }).ToListAsync(ct))
            .GroupBy(s => s.BuildingId).ToDictionary(g => g.Key, g => Math.Round(g.Average(s => s.Percent)));
        var today = DushanbeTime.Today(time.GetUtcNow().UtcDateTime);

        var blocks = deals.Select(d =>
        {
            string Money(decimal amount) => amount.ToString("N2", Invariant) + " " + d.Currency;
            string Date(DateOnly date) => date.ToString("dd.MM.yyyy", Invariant);
            var remaining = Math.Max(0m, d.Price - d.Paid);
            var percent = d.Price > 0 ? (int)Math.Clamp(Math.Floor(d.Paid * 100m / d.Price), 0m, 100m) : 0;
            var schedule = items.Where(i => i.DealId == d.Id).ToList();
            var open = schedule.Where(i => i.AmountPaid < i.AmountDue).ToList();
            var overdue = open.Where(i => i.DueDate < today).ToList();
            var next = open.FirstOrDefault(i => i.DueDate >= today);

            var lines = new List<string>
            {
                "=== BUYER CONTEXT ===",
                $"Name: {d.Name}",
                $"Apartment: №{d.Number}, floor {d.Floor}, {d.Rooms} rooms, {d.Area.ToString("0.##", Invariant)} m²",
                $"Price: {Money(d.Price)} | Paid: {Money(d.Paid)} ({percent}%) | Remaining: {Money(remaining)}",
                next is not null ? $"Next payment: {Date(next.DueDate)} — {Money(next.AmountDue - next.AmountPaid)}"
                    : schedule.Count == 0 ? "Next payment: no payment schedule yet" : "Next payment: none scheduled"
            };
            if (overdue.Count > 0)
                lines.Add($"Overdue: {overdue.Count} payment(s), {Money(overdue.Sum(i => i.AmountDue - i.AmountPaid))}, earliest since {Date(overdue[0].DueDate)}");
            lines.Add($"Deal stage: {d.Stage}");
            var building = $"Building: {d.Building}, {d.FloorsCount} floors, {d.Apartments} apartments";
            if (d.CompletionYear is { } year)
                building += $", delivery {year}";
            if (readiness.TryGetValue(d.BuildingId, out var ready))
                building += $", construction {ready}% complete";
            lines.Add(building);
            lines.Add("===================");
            return string.Join("\n", lines);
        });
        return string.Join("\n\n", blocks);
    }

    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

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
