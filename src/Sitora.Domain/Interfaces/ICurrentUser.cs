namespace Sitora.Domain.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsInRole(string role);
}
