namespace Sitora.Domain.Common;

// Удобства дома — фиксированный набор ключей: на сайте у каждого свой значок и перевод (ru/tg/en, frontend/js/i18n,
// ключи amen.<ключ>). Список дома задаёт контент-менеджер (PUT /buildings/{id}/amenities); чего нет в списке — сайт не пишет.
public static class BuildingAmenities
{
    public static readonly IReadOnlyList<string> Keys =
    [
        "underground_parking", // подземный паркинг
        "guest_parking",       // гостевая парковка
        "closed_yard",         // закрытый двор без машин
        "playground",          // детская площадка
        "football_field",      // мини-футбольное поле во дворе
        "sports_ground",       // спортивная площадка
        "security",            // охрана
        "cctv",                // видеонаблюдение
        "concierge",           // консьерж
        "elevators",           // лифты
        "landscaping",         // озеленение двора
        "commercial_floor",    // коммерческий первый этаж (магазины, кафе)
        "commercial",          // коммерческие помещения (магазины) на нижних этажах
        "storage_rooms",       // кладовые
        "barrier_free",        // безбарьерная среда
        "fitness",             // фитнес-зал
        "pool",                // бассейн
    ];

    private static readonly HashSet<string> Known = new(Keys, StringComparer.Ordinal);

    public static bool IsKnown(string? key) => key is not null && Known.Contains(key);

    // Порядок — как в Keys (на сайте список всегда в одном порядке), без повторов.
    public static List<string> Normalize(IEnumerable<string>? keys)
    {
        var set = new HashSet<string>(keys ?? [], StringComparer.Ordinal);
        return Keys.Where(set.Contains).ToList();
    }
}
