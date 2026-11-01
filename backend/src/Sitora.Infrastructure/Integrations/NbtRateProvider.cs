using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Integrations;

// Официальный курс USD с сайта НБТ (nbt.tj). Ответ — XML в windows-1251:
// <ValCurs Date="2026-10-31"><Valute ID="840"><CharCode>USD</CharCode><Nominal>1</Nominal><Value>9.2086</Value></Valute>…</ValCurs>.
// Сайт недоступен или ответ не разобран — предупреждение в лог и null: курс введёт бухгалтер (PUT /rates).
public class NbtRateProvider(IHttpClientFactory http, IConfiguration configuration, ILogger<NbtRateProvider> logger) : IRateProvider
{
    public const string HttpClientName = "nbt";
    // Адрес вида «?date=dd.MM.yyyy&tid=3&search=1» сайт не обслуживает (ошибка Bitrix); работает этот.
    public const string DefaultUrl = "https://nbt.tj/ru/kurs/export_xml.php";

    static NbtRateProvider() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public async Task<decimal?> GetUsdRateAsync(DateOnly date, CancellationToken ct = default)
    {
        var url = $"{configuration["Nbt:Url"] ?? DefaultUrl}?date={date:yyyy-MM-dd}&export=xmlout";
        try
        {
            var bytes = await http.CreateClient(HttpClientName).GetByteArrayAsync(url, ct);
            var rate = ParseUsdRate(bytes);
            if (rate is null)
                logger.LogWarning("НБТ: курс USD на {Date} не найден в ответе", date);
            return rate;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(e, "НБТ: не удалось получить курс на {Date}", date);
            return null;
        }
    }

    // Курс USD за 1 доллар (Value / Nominal); null, если в ответе его нет или ответ не XML.
    public static decimal? ParseUsdRate(byte[] xml)
    {
        XDocument doc;
        try
        {
            // Кодировку (windows-1251) XDocument берёт из объявления XML.
            using var stream = new MemoryStream(xml);
            doc = XDocument.Load(stream);
        }
        catch (Exception e) when (e is System.Xml.XmlException or ArgumentException)
        {
            return null;
        }

        var usd = doc.Descendants("Valute")
            .FirstOrDefault(v => string.Equals((string?)v.Element("CharCode") ?? (string?)v.Attribute("CharCode"), "USD", StringComparison.OrdinalIgnoreCase));
        if (usd is null || !TryDecimal((string?)usd.Element("Value"), out var value) || value <= 0)
            return null;
        var nominal = TryDecimal((string?)usd.Element("Nominal"), out var n) && n > 0 ? n : 1m;
        return Math.Round(value / nominal, 4);
    }

    private static bool TryDecimal(string? text, out decimal value) =>
        decimal.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
