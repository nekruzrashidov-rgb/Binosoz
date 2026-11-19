using Sitora.Domain.DTOs.Cash;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Reports;

namespace Sitora.Domain.Interfaces;

// Сообщение в личный чат Telegram (Markdown). Без токена бота или с chatId = 0 ничего не отправляется и не падает.
public interface ITelegramNotifier
{
    Task SendAsync(long chatId, string message, CancellationToken ct = default);

    // Одна попытка с ответом, чем кончилось: для очереди сообщений покупателю (повторы — задача Hangfire,
    // Blocked — бот заблокирован или чат удалён: чат отвязывается и больше туда не шлём).
    async Task<TelegramSendResult> TrySendAsync(long chatId, string message, CancellationToken ct = default)
    {
        await SendAsync(chatId, message, ct);
        return TelegramSendResult.Sent;
    }
}

public enum TelegramSendResult
{
    Sent,
    Blocked,
    Failed,
    NotConfigured
}

public interface ISmsSender
{
    Task SendAsync(string phone, string text, CancellationToken ct = default);
}

// Html — необязательная оформленная версия; Text уходит всегда, для почтовых программ без HTML.
// FromName — имя отправителя («Некруз Резиденс <no-reply@…>»): имя организации-застройщика; null — Email:FromName.
public record EmailMessage(string To, string Subject, string Text, string? Html = null, string? FromName = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface IRateProvider
{
    // Официальный курс USD к сомони от НБТ на дату; null, если получить не удалось.
    Task<decimal?> GetUsdRateAsync(DateOnly date, CancellationToken ct = default);
}

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

public interface IPdfGenerator
{
    byte[] RenderStatement(StatementData data);
    byte[] RenderSchedule(ScheduleDocumentData data);
    byte[] RenderCashReceipt(CashReceiptData data);
    byte[] RenderWeeklyReport(WeeklyReportData data);
}
