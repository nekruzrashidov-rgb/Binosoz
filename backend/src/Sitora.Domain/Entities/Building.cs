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

    public List<Floor> Floors { get; set; } = [];
}
