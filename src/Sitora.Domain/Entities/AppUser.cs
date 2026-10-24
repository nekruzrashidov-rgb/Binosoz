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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
