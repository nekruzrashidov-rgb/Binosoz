using Sitora.Domain.Common;
using System.Globalization;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Infrastructure.Services;

// Тексты сообщений покупателю на его языке (ru, tg, en): Telegram (Markdown), письмо и колокольчик кабинета.
// Только своё: номер квартиры, сумма своего платежа, дата. Паспортных данных, чужих сделок и текста чата здесь нет.
public static class BuyerTexts
{
    public const string Ru = "ru", Tg = "tg", En = "en";

    public static string Lang(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        Tg => Tg,
        En => En,
        _ => Ru
    };

    // По Accept-Language запроса: «tg-TJ» → tg, «en-US» → en, остальное — ru.
    public static string FromHeader(string? acceptLanguage)
    {
        var first = acceptLanguage?.Split(',', ';')[0].Trim().ToLowerInvariant() ?? "";
        return first.StartsWith(Tg) ? Tg : first.StartsWith(En) ? En : Ru;
    }

    private static string L(string lang, string ru, string tg, string en) => lang switch { Tg => tg, En => en, _ => ru };

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    // «12 500 TJS», «1 250,50 USD».
    public static string Money(decimal amount, Currency currency) =>
        (amount == decimal.Truncate(amount) ? amount.ToString("#,0", Russian) : amount.ToString("#,0.00", Russian))
        .Replace(' ', ' ').Replace(' ', ' ') + " " + currency;

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string Apt(string lang, UnitRef unit) => unit.InText(lang);

    // Заголовок напоминания (колокольчик, тема письма).
    public static string ReminderTitle(string lang, string evt) => evt switch
    {
        BuyerEvents.PaymentReminder or BuyerEvents.PaymentManual => L(lang, "Напоминание о платеже", "Ёдраскунӣ дар бораи пардохт", "Payment reminder"),
        BuyerEvents.PaymentDueToday => L(lang, "Сегодня срок платежа", "Имрӯз мӯҳлати пардохт", "Payment due today"),
        _ => L(lang, "Просроченный платёж", "Пардохти мӯҳлаташ гузашта", "Overdue payment")
    };

    // Сам текст напоминания (без разметки): колокольчик, письмо, основа сообщения в Telegram.
    public static string ReminderText(string lang, string evt, decimal amount, Currency currency, UnitRef apartment, DateOnly due, int overdueDays)
    {
        var money = Money(amount, currency);
        var apt = Apt(lang, apartment);
        var days = PaymentAlertService.ReminderDaysBefore;
        return evt switch
        {
            BuyerEvents.PaymentReminder => L(lang,
                $"Через {days} дня ({Date(due)}) платёж {money} по {apt}",
                $"Пас аз {days} рӯз ({Date(due)}) пардохти {money} барои {apt}",
                $"In {days} days ({Date(due)}) a payment of {money} for {apt} is due"),
            BuyerEvents.PaymentDueToday => L(lang,
                $"Сегодня срок платежа: {money} по {apt}",
                $"Имрӯз мӯҳлати пардохт: {money} барои {apt}",
                $"Today a payment is due: {money} for {apt}"),
            BuyerEvents.PaymentManual => overdueDays > 0
                ? L(lang,
                    $"Платёж {money} по {apt} просрочен на {overdueDays} дн. (срок — {Date(due)})",
                    $"Пардохти {money} барои {apt} {overdueDays} рӯз мӯҳлаташ гузаштааст (мӯҳлат — {Date(due)})",
                    $"The payment of {money} for {apt} is {overdueDays} days overdue (due {Date(due)})")
                : L(lang,
                    $"Платёж {money} по {apt}, срок — {Date(due)}",
                    $"Пардохти {money} барои {apt}, мӯҳлат — {Date(due)}",
                    $"Payment of {money} for {apt}, due {Date(due)}"),
            _ => L(lang,
                $"Платёж {money} по {apt} просрочен на {overdueDays} дн.",
                $"Пардохти {money} барои {apt} {overdueDays} рӯз мӯҳлаташ гузаштааст",
                $"The payment of {money} for {apt} is {overdueDays} days overdue")
        };
    }

    public static string ReminderIcon(string evt) => evt switch
    {
        BuyerEvents.PaymentReminder => "🟡",
        BuyerEvents.PaymentDueToday or BuyerEvents.PaymentManual => "🔔",
        _ => "🔴"
    };

    public static string OpenCabinet(string lang) => L(lang, "Открыть кабинет", "Кушодани кабинет", "Open my account");

    public static string StageName(string lang, DealStage stage) => stage switch
    {
        DealStage.Lead => L(lang, "заявка", "дархост", "request"),
        DealStage.Showing => L(lang, "показ квартиры", "нишон додани хона", "viewing"),
        DealStage.Booking => L(lang, "бронь", "брон", "booking"),
        DealStage.Contract => L(lang, "договор", "шартнома", "contract"),
        DealStage.Payment => L(lang, "оплата по графику", "пардохт аз рӯи ҷадвал", "payments by schedule"),
        DealStage.Completed => L(lang, "сделка завершена", "аҳд анҷом ёфт", "deal completed"),
        DealStage.Cancelled => L(lang, "сделка отменена", "аҳд бекор шуд", "deal cancelled"),
        _ => stage.ToString()
    };

    // ---------- письмо с напоминанием ----------

    public static string EmailIntro(string lang, string name) => L(lang, $"Здравствуйте, {name}!", $"Ассалому алейкум, {name}!", $"Hello, {name}!");

    public static string EmailFooter(string lang, string brand) => L(lang,
        $"Это письмо отправил застройщик {brand}: напоминания о платежах по вашему договору.",
        $"Ин мактубро созанда {brand} фиристод: ёдраскунӣ дар бораи пардохтҳо аз рӯи шартномаи шумо.",
        $"This letter was sent by {brand}: reminders about payments under your contract.");

    public static string Unsubscribe(string lang) => L(lang, "Отписаться от писем", "Аз мактубҳо даст кашидан", "Unsubscribe from e-mails");

    public static string AlreadyPaid(string lang) => L(lang,
        "Если вы уже оплатили, не обращайте внимания на это письмо.",
        "Агар шумо аллакай пардохт карда бошед, ба ин мактуб аҳамият надиҳед.",
        "If you have already paid, please ignore this letter.");

    // ---------- Telegram (Markdown) ----------

    private static string Link(string lang, string? cabinetUrl) =>
        cabinetUrl is null ? "" : $"\n[{OpenCabinet(lang)}]({cabinetUrl})";

    public static string TelegramReminder(string lang, string evt, string text, string? cabinetUrl) =>
        $"{ReminderIcon(evt)} *{ReminderTitle(lang, evt)}*\n{TelegramMessages.Escape(text)}{Link(lang, cabinetUrl)}";

    public static string BookingCreated(string lang, UnitRef apartment, DateTime expiresAtDushanbe, string? url) =>
        L(lang, "🏠 *Бронь создана*\n", "🏠 *Брон сохта шуд*\n", "🏠 *Booking created*\n")
        + L(lang, $"Бронь {Apt(lang, apartment)} действует до {expiresAtDushanbe:dd.MM.yyyy HH:mm} (Душанбе).",
            $"Брони {Apt(lang, apartment)} то {expiresAtDushanbe:dd.MM.yyyy HH:mm} (Душанбе) амал мекунад.",
            $"The booking of {Apt(lang, apartment)} lasts until {expiresAtDushanbe:dd.MM.yyyy HH:mm} (Dushanbe).") + Link(lang, url);

    public static string BookingExpiring(string lang, UnitRef apartment, DateTime expiresAtDushanbe, string? url) =>
        L(lang, "⏳ *Бронь скоро закончится*\n", "⏳ *Брон ба зудӣ тамом мешавад*\n", "⏳ *Booking ends soon*\n")
        + L(lang, $"Бронь {Apt(lang, apartment)} закончится {expiresAtDushanbe:dd.MM.yyyy} в {expiresAtDushanbe:HH:mm} (Душанбе). Продлить — напишите менеджеру.",
            $"Брони {Apt(lang, apartment)} {expiresAtDushanbe:dd.MM.yyyy} соати {expiresAtDushanbe:HH:mm} (Душанбе) тамом мешавад. Барои дароз кардан ба менеҷер нависед.",
            $"The booking of {Apt(lang, apartment)} ends on {expiresAtDushanbe:dd.MM.yyyy} at {expiresAtDushanbe:HH:mm} (Dushanbe). To extend it, write to the manager.") + Link(lang, url);

    public static string BookingReleased(string lang, UnitRef apartment, string? url) =>
        L(lang, "🔓 *Бронь снята*\n", "🔓 *Брон бекор шуд*\n", "🔓 *Booking released*\n")
        + L(lang, $"Бронь {Apt(lang, apartment)} больше не действует.", $"Брони {Apt(lang, apartment)} дигар амал намекунад.",
            $"The booking of {Apt(lang, apartment)} is no longer active.") + Link(lang, url);

    public static string DealStageChanged(string lang, UnitRef apartment, DealStage stage, string? url) =>
        L(lang, "📋 *Сделка: новый этап*\n", "📋 *Аҳд: марҳилаи нав*\n", "📋 *Deal: new stage*\n")
        + L(lang, $"По {Apt(lang, apartment)}: {StageName(lang, stage)}.", $"Барои {Apt(lang, apartment)}: {StageName(lang, stage)}.",
            $"For {Apt(lang, apartment)}: {StageName(lang, stage)}.") + Link(lang, url);

    public static string PaymentReceived(string lang, UnitRef apartment, decimal amount, Currency currency, DateOnly paidAt, string? url) =>
        L(lang, "✅ *Платёж принят*\n", "✅ *Пардохт қабул шуд*\n", "✅ *Payment received*\n")
        + L(lang, $"{Money(amount, currency)} по {Apt(lang, apartment)}, {Date(paidAt)}. Спасибо!",
            $"{Money(amount, currency)} барои {Apt(lang, apartment)}, {Date(paidAt)}. Ташаккур!",
            $"{Money(amount, currency)} for {Apt(lang, apartment)}, {Date(paidAt)}. Thank you!") + Link(lang, url);

    public static string DocumentAdded(string lang, UnitRef apartment, string? url) =>
        L(lang, "📄 *Новый документ*\n", "📄 *Ҳуҷҷати нав*\n", "📄 *New document*\n")
        + L(lang, $"В кабинете новый документ по {Apt(lang, apartment)}.", $"Дар кабинет ҳуҷҷати нав барои {Apt(lang, apartment)}.",
            $"A new document for {Apt(lang, apartment)} is in your account.") + Link(lang, url);

    public static string ChatMessage(string lang, UnitRef apartment, string? url) =>
        L(lang, "💬 *Новое сообщение*\n", "💬 *Паёми нав*\n", "💬 *New message*\n")
        + L(lang, $"Менеджер написал вам по {Apt(lang, apartment)}.", $"Менеҷер ба шумо оид ба {Apt(lang, apartment)} навишт.",
            $"The manager wrote to you about {Apt(lang, apartment)}.") + Link(lang, url);

    // ---------- ответы бота ----------

    public static string BotLinked(string lang, string brand) => BotLinkedRaw(lang, brand).Replace("  ", " ");

    private static string BotLinkedRaw(string lang, string brand) => L(lang,
        $"✅ Готово! Уведомления {TelegramMessages.Escape(brand)} будут приходить сюда: платежи, бронь, документы, сообщения менеджера.\nОтключить — команда /stop или в личном кабинете.",
        $"✅ Тайёр! Огоҳиҳои {TelegramMessages.Escape(brand)} ба ин ҷо меоянд: пардохтҳо, брон, ҳуҷҷатҳо, паёмҳои менеҷер.\nХомӯш кардан — фармони /stop ё дар кабинети шахсӣ.",
        $"✅ Done! {TelegramMessages.Escape(brand)} notifications will come here: payments, booking, documents, manager's messages.\nTo turn them off — /stop or in your account.");

    public static string BotBadLink(string lang) => L(lang,
        "Ссылка устарела или уже использована. Откройте личный кабинет и нажмите «Получать уведомления в Telegram» ещё раз.",
        "Пайванд кӯҳна шудааст ё аллакай истифода шудааст. Кабинети шахсиро кушоед ва боз «Огоҳиҳоро дар Telegram гирифтан»-ро пахш кунед.",
        "The link has expired or was already used. Open your account and press «Get notifications in Telegram» again.");

    public static string BotHello(string lang) => L(lang,
        "Здравствуйте! Чтобы получать уведомления о платежах и брони, откройте личный кабинет на сайте застройщика и нажмите «Получать уведомления в Telegram».",
        "Ассалому алейкум! Барои гирифтани огоҳиҳо дар бораи пардохт ва брон кабинети шахсиро дар сомонаи созанда кушоед ва «Огоҳиҳоро дар Telegram гирифтан»-ро пахш кунед.",
        "Hello! To get payment and booking notifications, open your account on the developer's site and press «Get notifications in Telegram».");

    public static string BotStopped(string lang) => L(lang,
        "Уведомления отключены, этот чат отвязан. Включить снова — в личном кабинете.",
        "Огоҳиҳо хомӯш шуданд, ин чат ҷудо карда шуд. Аз нав фаъол кардан — дар кабинети шахсӣ.",
        "Notifications are off, this chat is unlinked. To turn them on again — in your account.");

    // ---------- страница отписки ----------

    public static string UnsubscribeAsk(string lang) => L(lang,
        "Больше не присылать напоминания о платежах на почту? В кабинете и в Telegram они останутся.",
        "Дигар ёдраскуниҳои пардохтро ба почта нафиристем? Дар кабинет ва Telegram онҳо мемонанд.",
        "Stop sending payment reminders by e-mail? They stay in your account and in Telegram.");

    public static string UnsubscribeDone(string lang) => L(lang,
        "Готово: напоминания на почту больше не придут. Включить снова — в личном кабинете, «Настройки».",
        "Тайёр: ёдраскуниҳо дигар ба почта намеоянд. Аз нав фаъол кардан — дар кабинети шахсӣ, «Танзимот».",
        "Done: reminders will no longer come by e-mail. To turn them on again — in your account, «Settings».");

    public static string UnsubscribeBad(string lang) => L(lang,
        "Ссылка недействительна или устарела. Отключить письма можно в личном кабинете, «Настройки».",
        "Пайванд нодуруст ё кӯҳна аст. Мактубҳоро дар кабинети шахсӣ, «Танзимот» хомӯш кардан мумкин аст.",
        "The link is invalid or expired. You can turn e-mails off in your account, «Settings».");
}
