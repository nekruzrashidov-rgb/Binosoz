using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Cash;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using QuestDocument = QuestPDF.Fluent.Document;

namespace Sitora.Infrastructure.Integrations;

public class QuestPdfGenerator : IPdfGenerator
{
    // Пробел как разделитель разрядов и запятая для копеек, как принято в документах.
    private static readonly NumberFormatInfo MoneyFormat = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = "," };

    static QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Используем только встроенный в QuestPDF шрифт Lato: в нём есть кириллица и таджикские буквы,
        // и документ выглядит одинаково на любой машине. Редкий символ без глифа заменяется, а не роняет акт.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public static string Money(decimal amount) => amount.ToString("#,0.00", MoneyFormat);

    public byte[] RenderStatement(StatementData data)
    {
        return QuestDocument.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily("Lato"));

            page.Header().Column(column =>
            {
                column.Item().Text("Акт сверки взаиморасчётов").FontSize(16).Bold();
                column.Item().Text($"на {data.AsOf:dd.MM.yyyy}");
            });

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(4);
                column.Item().Text(t => { t.Span("Покупатель: ").SemiBold(); t.Span(data.CustomerName); });
                column.Item().Text(t =>
                {
                    t.Span("Объект: ").SemiBold();
                    t.Span($"{data.BuildingName}, этаж {data.FloorNumber}, кв. №{data.ApartmentNumber}");
                });
                column.Item().Text(t => { t.Span("Сделка: ").SemiBold(); t.Span(data.DealId.ToString()); });
                column.Item().Text(t => { t.Span("Валюта договора: ").SemiBold(); t.Span(data.Currency.ToString()); });

                column.Item().PaddingTop(10).Text("Платежи").FontSize(12).Bold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        foreach (var title in new[] { "Дата", "Сумма", "Курс", $"В валюте договора", "Вид" })
                            header.Cell().BorderBottom(1).PaddingBottom(3).Text(title).SemiBold();
                    });

                    foreach (var payment in data.Payments)
                    {
                        table.Cell().PaddingVertical(2).Text($"{payment.PaidAt:dd.MM.yyyy}");
                        table.Cell().PaddingVertical(2).Text($"{Money(payment.Amount)} {payment.Currency}");
                        table.Cell().PaddingVertical(2).Text(payment.RateUsed?.ToString("0.0000", MoneyFormat) ?? "—");
                        table.Cell().PaddingVertical(2).Text(Money(payment.AmountInDealCurrency));
                        table.Cell().PaddingVertical(2).Text(payment.IsReversal ? "сторно" : "оплата");
                    }
                });

                if (data.Payments.Count == 0)
                    column.Item().Text("Платежей на эту дату нет").Italic();

                column.Item().PaddingTop(12).Text("Итоги").FontSize(12).Bold();
                Total(column, "Сумма договора", data.ContractTotal, data.Currency);
                Total(column, "Начислено по графику", data.Accrued, data.Currency);
                Total(column, "Оплачено", data.Paid, data.Currency);
                Total(column, "Задолженность", data.Debt, data.Currency);
                Total(column, "Остаток по договору", data.Remaining, data.Currency);
            });

            Footer(page, data.GeneratedAtUtc);
        })).GeneratePdf();
    }

    public byte[] RenderSchedule(ScheduleDocumentData data)
    {
        var schedule = data.Schedule;
        var equivalent = schedule.Equivalent;

        return QuestDocument.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily("Lato"));

            page.Header().Column(column =>
            {
                column.Item().Text("График платежей").FontSize(16).Bold();
                column.Item().Text($"по состоянию на {data.AsOf:dd.MM.yyyy}");
            });

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(4);
                DealHeaderLines(column, data.Deal, schedule.Currency);
                column.Item().Text(t =>
                {
                    t.Span("Условия: ").SemiBold();
                    t.Span($"первоначальный взнос {schedule.DownPaymentPct:0.##}%, {schedule.Months} мес., " +
                           $"платёж {schedule.PaymentDay}-го числа, начало {schedule.StartDate:dd.MM.yyyy}");
                });

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(28);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        if (equivalent is not null)
                            c.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        var titles = new List<string> { "№", "Дата", "Вид", $"К оплате, {schedule.Currency}", $"Оплачено, {schedule.Currency}", "Статус" };
                        if (equivalent is not null)
                            titles.Add($"≈ {equivalent.Currency}*");
                        foreach (var title in titles)
                            header.Cell().BorderBottom(1).PaddingBottom(3).Text(title).SemiBold();
                    });

                    var number = 1;
                    foreach (var item in schedule.Items)
                    {
                        table.Cell().PaddingVertical(2).Text($"{number++}");
                        table.Cell().PaddingVertical(2).Text($"{item.DueDate:dd.MM.yyyy}");
                        table.Cell().PaddingVertical(2).Text(item.Kind == ScheduleItemKind.DownPayment ? "взнос" : "месячный");
                        table.Cell().PaddingVertical(2).Text(Money(item.AmountDue));
                        table.Cell().PaddingVertical(2).Text(Money(item.AmountPaid));
                        table.Cell().PaddingVertical(2).Text(StatusText(item));
                        if (equivalent is not null)
                            table.Cell().PaddingVertical(2).Text(item.AmountDueEquivalent is { } eq ? Money(eq) : "—");
                    }
                });

                column.Item().PaddingTop(12).Text("Итоги").FontSize(12).Bold();
                Total(column, "Сумма договора", schedule.Total, schedule.Currency);
                Total(column, "Оплачено", schedule.Paid, schedule.Currency);
                Total(column, "Остаток", schedule.Remaining, schedule.Currency);
                Total(column, "Просрочено", schedule.Overdue, schedule.Currency);

                if (equivalent is not null)
                {
                    column.Item().PaddingTop(8).Text(
                        $"* Остаток ≈ {Money(equivalent.Remaining)} {equivalent.Currency}. {equivalent.Note}").FontSize(8).Italic();
                }
            });

            Footer(page, data.GeneratedAtUtc);
        })).GeneratePdf();
    }

    // Кассовая квитанция на A5: приходный или расходный ордер; у сторно — красная полоса «СТОРНО» поверх.
    public byte[] RenderCashReceipt(CashReceiptData data)
    {
        var tx = data.Transaction;
        var income = tx.Direction == CashDirection.Income;
        return QuestDocument.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily("Lato"));

            page.Header().Column(column =>
            {
                column.Item().Text(data.OrganizationName).SemiBold();
                column.Item().Text(data.BuildingName).FontSize(9);
                column.Item().PaddingTop(8).Text(income ? "Приходный кассовый ордер" : "Расходный кассовый ордер").FontSize(14).Bold();
                column.Item().Text($"№ {data.Number} от {tx.OperationDate:dd.MM.yyyy}");
            });

            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(5);
                Line(column, "Статья", data.CategoryText);
                Line(column, "Основание", tx.Description);
                if (data.DealText is not null)
                    Line(column, "Сделка", data.DealText);
                if (data.ReversalOfNumber is not null)
                    Line(column, "Сторно операции", $"№ {data.ReversalOfNumber}");
                column.Item().PaddingTop(6).Text(t =>
                {
                    t.Span("Сумма: ").SemiBold();
                    t.Span($"{Money(Math.Abs(tx.Amount))} {tx.Currency}").FontSize(14).Bold();
                });
                if (tx.Currency != Currency.TJS)
                    Line(column, "В сомони по курсу на дату операции", $"{Money(Math.Abs(tx.AmountTjs))} TJS");
                Line(column, "Внёс в кассовую книгу", tx.CreatedByName);

                column.Item().PaddingTop(28).Row(row =>
                {
                    row.RelativeItem().Text("Кассир ____________________");
                    row.RelativeItem().AlignRight().Text(income ? "Принято от ____________________" : "Получил ____________________");
                });
            });

            if (tx.IsReversal)
                page.Foreground().AlignMiddle().AlignCenter().Rotate(-20)
                    .Background(Colors.Red.Medium).PaddingVertical(6).PaddingHorizontal(40)
                    .Text("СТОРНО").FontSize(32).Bold().FontColor(Colors.White);

            Footer(page, data.GeneratedAtUtc);
        })).GeneratePdf();
    }

    // Еженедельный отчёт владельцу на A4: сводка карточками и топ-5 должников.
    public byte[] RenderWeeklyReport(WeeklyReportData data)
    {
        const string gold = "#c9a227";
        var s = data.Summary;
        return QuestDocument.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily("Lato"));

            page.Header().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Binosoz").FontSize(22).Bold().FontColor(gold);
                    row.RelativeItem().AlignRight().AlignBottom().Text(s.OrganizationName).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                column.Item().PaddingTop(6).Text($"Еженедельный отчёт: {s.BuildingName}").FontSize(15).Bold();
                column.Item().Text($"Неделя {s.PeriodStart:dd.MM.yyyy} — {s.PeriodEnd:dd.MM.yyyy}").FontColor(Colors.Grey.Darken2);
                column.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(gold);
            });

            page.Content().PaddingVertical(14).Column(column =>
            {
                column.Spacing(14);
                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    Card(row.RelativeItem(), "Поступления по кассе", $"{Money(s.IncomeTjs)} TJS", Colors.Green.Darken2);
                    Card(row.RelativeItem(), "Расходы по кассе", $"{Money(s.ExpenseTjs)} TJS", Colors.Red.Darken2);
                    Card(row.RelativeItem(), "Чистая прибыль", $"{Money(s.ProfitTjs)} TJS", s.ProfitTjs < 0 ? Colors.Red.Darken2 : Colors.Black);
                });
                column.Item().Row(row =>
                {
                    row.Spacing(10);
                    Card(row.RelativeItem(), "Новых сделок", s.NewDeals.ToString(CultureInfo.InvariantCulture), Colors.Black);
                    Card(row.RelativeItem(), $"Просрочек на {s.AsOf:dd.MM.yyyy}", s.OverdueItems.ToString(CultureInfo.InvariantCulture), s.OverdueItems > 0 ? Colors.Red.Darken2 : Colors.Black);
                    Card(row.RelativeItem(), "Готовность стройки", $"{s.ConstructionPercent}%", Colors.Black);
                });
                column.Item().Text($"Платежей покупателей за неделю: {data.Payments.Count(p => !p.IsReversal)}, операций по кассе: {data.Cash.Count}. Подробности — в Excel-файле отчёта.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);

                column.Item().Text($"Топ-{data.TopDebtors.Count} должников").FontSize(12).Bold();
                if (data.TopDebtors.Count == 0)
                    column.Item().Text("Просроченных платежей нет.");
                else
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(1.4f); c.RelativeColumn(2.6f); c.RelativeColumn(1.6f); });
                        table.Header(h =>
                        {
                            foreach (var title in new[] { "Покупатель", "Кв.", "Просрочено", "Дней" })
                                h.Cell().Background(gold).Padding(4).Text(title).SemiBold().FontColor(Colors.White);
                        });
                        foreach (var d in data.TopDebtors)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(d.Customer);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(d.Apartment.ToString(CultureInfo.InvariantCulture));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{Money(d.Left)} {d.Currency}");
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(d.MaxDaysOverdue.ToString(CultureInfo.InvariantCulture));
                        }
                    });
            });

            Footer(page, data.GeneratedAtUtc);
        })).GeneratePdf();
    }

    private static void Card(IContainer container, string title, string value, string color) =>
        container.Border(0.75f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
        {
            c.Item().Text(title).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(3).Text(value).FontSize(14).Bold().FontColor(color);
        });

    private static void Line(ColumnDescriptor column, string title, string value) =>
        column.Item().Text(t => { t.Span($"{title}: ").SemiBold(); t.Span(value); });

    private static void DealHeaderLines(ColumnDescriptor column, DealHeader deal, Currency currency)
    {
        column.Item().Text(t => { t.Span("Покупатель: ").SemiBold(); t.Span(deal.CustomerName); });
        column.Item().Text(t =>
        {
            t.Span("Объект: ").SemiBold();
            t.Span($"{deal.BuildingName}, этаж {deal.FloorNumber}, кв. №{deal.ApartmentNumber}");
        });
        column.Item().Text(t => { t.Span("Сделка: ").SemiBold(); t.Span(deal.DealId.ToString()); });
        column.Item().Text(t => { t.Span("Валюта договора: ").SemiBold(); t.Span(currency.ToString()); });
    }

    private static string StatusText(ScheduleItemResponse item) => item.Status switch
    {
        ScheduleItemStatus.Paid => "оплачен",
        _ when item.IsOverdue => "просрочен",
        ScheduleItemStatus.PartiallyPaid => "частично",
        _ => "ожидается"
    };

    private static void Footer(PageDescriptor page, DateTime generatedAtUtc) =>
        page.Footer().AlignCenter().Text(
            $"Сформировано {DushanbeTime.ToLocal(generatedAtUtc):dd.MM.yyyy HH:mm} (Душанбе)").FontSize(8);

    private static void Total(ColumnDescriptor column, string title, decimal amount, Currency currency) =>
        column.Item().Text(t =>
        {
            t.Span($"{title}: ");
            t.Span($"{Money(amount)} {currency}").SemiBold();
        });
}
