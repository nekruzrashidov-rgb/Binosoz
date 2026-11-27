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

    // Правило цены (этап «Цена за м² по этажу»): ступени по этажам и цена м² пентхаусов последнего этажа, в валюте
    // PriceCurrency. Меняет руководитель (PUT /buildings/{id}/price-rule), к свободным квартирам — «Пересчитать цены».
    public List<PriceStep>? PriceSteps { get; set; }
    public decimal? PenthousePricePerM2 { get; set; }
    public Sitora.Domain.Enums.Currency PriceCurrency { get; set; } = Sitora.Domain.Enums.Currency.TJS;
    // Коммерция 1–2 этажа — своя цена м²; машино-место паркинга — своя цена за место (не от площади).
    public decimal? CommercialPricePerM2 { get; set; }
    public decimal? ParkingSpotPrice { get; set; }

    // Лифтов в блоке (на сайте — «Лифтов: 3», в 3D — шахты в ядре); не задано — не показывается.
    public int? ElevatorsCount { get; set; }

    // На публичном сайте: false — дом скрыт из каталога, бренда, AI-помощника гостя и «Зелёного эффекта»; сотрудники видят его
    // в CRM, покупатель — свою сделку, график, платежи и документы. Меняется через PUT /buildings/{id}/published (AuditLog).
    public bool IsPublished { get; set; } = true;

    public List<Floor> Floors { get; set; } = [];
}
