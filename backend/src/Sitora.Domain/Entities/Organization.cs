namespace Sitora.Domain.Entities;

// Владелец-компания (застройщик). Ей принадлежат дома, сотрудники, клиенты и заявки.
public class Organization : BaseEntity
{
    // Организация, к которой при переходе на мультиарендность привязаны все прежние дома и сотрудники.
    public static readonly Guid DefaultId = new("01a20000-0000-7000-8000-000000000001");
    public const string DefaultSlug = "sitora";

    public string Name { get; set; } = string.Empty;

    // Короткое имя для адреса публичного сайта застройщика: латиница, цифры, дефис.
    // Оно же поддомен: richtower → richtower.sitora.tj.
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Бренд на сайте и в письмах (white-label).
    // LogoUrl — адрес логотипа для сайта; загруженный файл лежит в хранилище под LogoFileKey.
    public string? LogoUrl { get; set; }
    public string? LogoFileKey { get; set; }
    public string? LogoContentType { get; set; }
    public string? PrimaryColor { get; set; }
    public string? Phone { get; set; }
    public string? Telegram { get; set; }
    public string? Address { get; set; }

    // Свой домен застройщика (например, richtower.tj). Задаёт только создатель платформы.
    public string? CustomDomain { get; set; }

    public List<Building> Buildings { get; set; } = [];
}

// Дом, к которому у сотрудника есть доступ. У ObjectAdmin ровно одна такая запись;
// сотрудник без записей видит все дома своей организации.
public class UserBuilding
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;
}
