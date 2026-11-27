using Sitora.Domain.Enums;

namespace Sitora.Domain.Common;

// Номер помещения для людей: в блоке ЖК — «Блок A, кв. 45» (кратко «A-45»), магазин — «Блок A, пом. Н-3» («A-Н-3»),
// машино-место — «Блок A, м/м М-12» («A-М-12»). Дом без блока (прежний «Некруз Резиденс») — как раньше: «кв. №1201».
public static class UnitNames
{
    // Код один на всех языках (как табличка на двери): Н — нежилое помещение, М — машино-место.
    private static string Prefix(ApartmentKind kind) => kind switch
    {
        ApartmentKind.Commercial => "Н-",
        ApartmentKind.Parking => "М-",
        _ => ""
    };

    public static string Code(ApartmentKind kind, int number) => Prefix(kind) + number;

    // Кратко: «A-45»; без блока — просто номер.
    public static string Short(string? block, ApartmentKind kind, int number) =>
        string.IsNullOrEmpty(block) ? Code(kind, number) : block + "-" + Code(kind, number);

    // Полностью: «Блок A, кв. 45»; без блока — «кв. №45».
    public static string Full(string? block, ApartmentKind kind, int number, string lang = "ru")
    {
        var code = Code(kind, number);
        var unit = (kind, lang) switch
        {
            (ApartmentKind.Commercial, "tg") => "бинои " + code,
            (ApartmentKind.Commercial, "en") => "premises " + code,
            (ApartmentKind.Commercial, _) => "пом. " + code,
            (ApartmentKind.Parking, "tg") => "ҷойи мошин " + code,
            (ApartmentKind.Parking, "en") => "parking space " + code,
            (ApartmentKind.Parking, _) => "м/м " + code,
            (_, "tg") => string.IsNullOrEmpty(block) ? "хонаи №" + code : "хонаи " + code,
            (_, "en") => "apt. " + code,
            _ => string.IsNullOrEmpty(block) ? "кв. №" + code : "кв. " + code
        };
        if (string.IsNullOrEmpty(block))
            return unit;
        var name = lang switch { "tg" => "Блоки ", "en" => "Block ", _ => "Блок " } + block;
        return name + ", " + unit;
    }
}

// Помещение для текстов уведомлений: номер, блок и вид. Из числа — квартира дома без блока (как раньше).
public readonly record struct UnitRef(int Number, string? Block = null, ApartmentKind Kind = ApartmentKind.Residential)
{
    public static implicit operator UnitRef(int number) => new(number);

    // Внутри предложения: «кв. 45 в блоке A», «пом. Н-3 в блоке A»; без блока — «кв. №45».
    public string InText(string lang = "ru")
    {
        var unit = UnitNames.Full(null, Kind, Number, lang);
        if (string.IsNullOrEmpty(Block))
            return unit;
        unit = unit.Replace("№", "", StringComparison.Ordinal);
        return unit + lang switch { "tg" => " дар блоки ", "en" => " in Block ", _ => " в блоке " } + Block;
    }

    public string Full(string lang = "ru") => UnitNames.Full(Block, Kind, Number, lang);

    public override string ToString() => UnitNames.Short(Block, Kind, Number);
}
