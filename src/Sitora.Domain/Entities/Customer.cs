namespace Sitora.Domain.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }

    // Зашифровано EncryptionService; в открытом виде в базе не хранится.
    public string? PassportDataEncrypted { get; set; }

    // Учётная запись покупателя для личного кабинета.
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public List<Deal> Deals { get; set; } = [];
}
