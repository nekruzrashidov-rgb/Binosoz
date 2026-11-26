namespace Sitora.Domain.Entities;

public class Building : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string NameRu { get; set; } = string.Empty;
    public string NameTg { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public int FloorsCount { get; set; }
    public string? DescriptionRu { get; set; }
    public string? DescriptionTg { get; set; }
    // Английские тексты для сайта на EN (A-10); не заданы — сайт показывает русский с пометкой.
    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? ModelUrl { get; set; }

    // Год сдачи и сколько квартир задумано в доме — задаёт владелец при создании; заведённых квартир может быть меньше.
    public int? CompletionYear { get; set; }
    public int? PlannedApartments { get; set; }

    // Эко-паспорт (null — не заполнен). Меняется только через PUT /buildings/{id}/eco.
    public BuildingEcoInfo? Eco { get; set; }

    // Удобства дома — ключи из Sitora.Domain.Common.BuildingAmenities (на сайте — со значками и переводами).
    // Меняются только через PUT /buildings/{id}/amenities.
    public List<string> Amenities { get; set; } = [];

    // Точка дома на карте (WGS 84) и высота этажа от пола до пола в метрах — для 3D-модели на сайте:
    // окружение из OpenStreetMap и метки расстояний есть только при заданных координатах; высота не задана — сайт
    // считает 3,0 м и пишет «оценка». Меняются только через PUT /buildings/{id}/location.
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? FloorHeight { get; set; }

    // Блок ЖК на общем участке (docs/DECISIONS.md, «Rich Tower: 3 блока»). Контур в плане — многоугольник в метрах
    // (точка [x, y] или [x, y, r] — радиус скругления угла), место на участке, уступы верхних этажей; пока у застройщика
    // нет проекта, это концепт, и сайт так и подписывает. Не задано — сайт рисует обычный прямоугольный дом.
    // Меняются только через PUT /buildings/{id}/shape (проверка контура, пересечений и разрыва между блоками, AuditLog).
    public List<double[]>? Footprint { get; set; }
    public SitePlacement? SitePosition { get; set; }
    public List<BuildingSetback>? Setbacks { get; set; }
    // Буква блока (A, B, C — до 4 символов) и порядок на сайте; дом без буквы — не блок ЖК.
    public string? BlockCode { get; set; }
    public int SortOrder { get; set; }


    public List<Floor> Floors { get; set; } = [];
}
