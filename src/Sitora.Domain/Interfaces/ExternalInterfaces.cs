using Sitora.Domain.DTOs.Payments;

namespace Sitora.Domain.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string phone, string text, CancellationToken ct = default);
}

public record EmailMessage(string To, string Subject, string Text);

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
}
