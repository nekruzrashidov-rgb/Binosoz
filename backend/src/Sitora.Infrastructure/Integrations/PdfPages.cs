using System.Text;
using System.Text.RegularExpressions;

namespace Sitora.Infrastructure.Integrations;

// Число страниц PDF без сторонних библиотек: значение /Count корневого узла /Type /Pages (у корня оно наибольшее).
// QuestPDF и большинство программ пишут дерево страниц открытым текстом. Если словарь страниц сжат (object streams)
// и /Count не найден — null: страницы такого файла не считаются, а не угадываются.
public static partial class PdfPages
{
    public static int? Count(byte[]? pdf)
    {
        if (pdf is null || pdf.Length < 5 || !(pdf[0] == '%' && pdf[1] == 'P' && pdf[2] == 'D' && pdf[3] == 'F'))
            return null;
        var text = Encoding.Latin1.GetString(pdf);
        var max = 0;
        foreach (Match m in PagesDictionary().Matches(text))
        {
            var count = m.Groups["a"].Success ? m.Groups["a"].Value : m.Groups["b"].Value;
            if (int.TryParse(count, out var n) && n > max)
                max = n;
        }
        return max > 0 ? max : null;
    }

    // Словарь << … /Type /Pages … /Count N … >> в любом порядке ключей (без вложенных словарей между ними).
    [GeneratedRegex(@"<<(?:(?!>>).)*?/Type\s*/Pages\b(?:(?!>>).)*?/Count\s+(?<a>\d+)|<<(?:(?!>>).)*?/Count\s+(?<b>\d+)(?:(?!>>).)*?/Type\s*/Pages\b", RegexOptions.Singleline)]
    private static partial Regex PagesDictionary();
}
