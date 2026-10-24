namespace Sitora.Domain.Enums;

public enum UserRole
{
    Admin,
    Manager,
    Accountant,
    Content,
    Customer
}

public static class RoleNames
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Manager = nameof(UserRole.Manager);
    public const string Accountant = nameof(UserRole.Accountant);
    public const string Content = nameof(UserRole.Content);
    public const string Customer = nameof(UserRole.Customer);

    public static readonly string[] All = Enum.GetNames<UserRole>();
}
