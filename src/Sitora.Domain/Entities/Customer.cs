namespace Sitora.Domain.Entities;

// Клиент конкретного застройщика. Один человек у двух застройщиков — две записи с общей учётной записью.
public class Customer : BaseEntity
{
    public Guid OrganizationId { get; set; }

    // Дом, где клиента завели; по нему клиента видят сотрудники, у которых доступ только к части домов.
    public Guid? HomeBuildingId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    // Нормализованная почта (trim, нижний регистр): по ней покупатель входит в кабинет.
    public string? Email { get; set; }

    // Зашифровано EncryptionService; в открытом виде в базе не хранится.
    public string? PassportDataEncrypted { get; set; }

    // Учётная запись покупателя для личного кабинета.
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public List<Deal> Deals { get; set; } = [];
}
