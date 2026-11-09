namespace Sitora.Domain.DTOs.Sales;

// Импорт клиентов из Excel: столбцы Имя | Телефон | Email | Комментарий, первая строка — заголовки.
// Status строки: new — будет добавлен, duplicate — телефон уже есть (в базе или выше в файле), invalid — ошибка в данных.
public record ClientImportRow(int Row, string Name, string Phone, string? Email, string? Comment, string Status, string? Error);

// DryRun = true — только предпросмотр, в базу ничего не пишется.
public record ClientImportResult(bool DryRun, int Added, int Skipped, int Invalid, IReadOnlyList<ClientImportRow> Rows);

public record MessageTemplateResponse(Guid Id, string Name, string Text, string Type, DateTime UpdatedAt);

public record MessageTemplateRequest(string Name, string Text, string Type);

// Аналитика продаж: сделки по месяцам за 12 месяцев и воронка Заявки → Брони → Сделки → Завершены (проценты — от заявок).
public record SalesMonthRow(string Month, int Deals, int Completed);

public record FunnelStep(string Key, int Count, decimal Percent);

public record SalesAnalyticsResponse(IReadOnlyList<SalesMonthRow> Months, IReadOnlyList<FunnelStep> Funnel);

// Карточка проекта для руководителя: стройка, продажи и деньги по дому. Суммы — в сомони (USD по текущему курсу).
public record ProjectCardResponse(
    Guid BuildingId,
    string Name,
    string Developer,
    int ConstructionPercent,
    string? NextStage,
    int? CompletionYear,
    int TotalApartments,
    int Sold,
    int Booked,
    int Free,
    decimal Revenue,
    decimal Plan,
    string Currency);
