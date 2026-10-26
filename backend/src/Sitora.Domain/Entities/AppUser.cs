using Microsoft.AspNetCore.Identity;

namespace Sitora.Domain.Entities;

public class AppUser : IdentityUser<Guid>, IHasTimestamps
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Организация сотрудника; у покупателей и создателя платформы её нет.
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    // Дома, доступные сотруднику; пусто — все дома организации.
    public List<UserBuilding> Buildings { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
