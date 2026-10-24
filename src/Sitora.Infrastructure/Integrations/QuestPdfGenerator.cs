using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Payments;
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
                Total(column, "Сумма договора", data.ContractTotal, data);
                Total(column, "Начислено по графику", data.Accrued, data);
                Total(column, "Оплачено", data.Paid, data);
                Total(column, "Задолженность", data.Debt, data);
                Total(column, "Остаток по договору", data.Remaining, data);
            });

            page.Footer().AlignCenter().Text(
                $"Сформировано {DushanbeTime.ToLocal(data.GeneratedAtUtc):dd.MM.yyyy HH:mm} (Душанбе)").FontSize(8);
        })).GeneratePdf();
    }

    private static void Total(ColumnDescriptor column, string title, decimal amount, StatementData data) =>
        column.Item().Text(t =>
        {
            t.Span($"{title}: ");
            t.Span($"{Money(amount)} {data.Currency}").SemiBold();
        });
}
