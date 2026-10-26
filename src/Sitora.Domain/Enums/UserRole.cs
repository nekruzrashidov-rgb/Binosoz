namespace Sitora.Domain.Enums;

public enum UserRole
{
    // Создатель платформы: видит все организации, на странице входа не показывается.
    PlatformOwner,

    // Владелец (застройщик): все дома своей организации.
    Owner,

    // Админ объекта: ровно один назначенный дом.
    ObjectAdmin,

    // Должности внутри организации или дома.
    Manager,
    Accountant,
    Content,

    Customer
}

public static class RoleNames
{
    public const string PlatformOwner = nameof(UserRole.PlatformOwner);
    public const string Owner = nameof(UserRole.Owner);
    public const string ObjectAdmin = nameof(UserRole.ObjectAdmin);
    public const string Manager = nameof(UserRole.Manager);
    public const string Accountant = nameof(UserRole.Accountant);
    public const string Content = nameof(UserRole.Content);
    public const string Customer = nameof(UserRole.Customer);

    public static readonly string[] All = Enum.GetNames<UserRole>();

    // Руководители: управляют своей организацией (Owner) или своим домом (ObjectAdmin); PlatformOwner — всем.
    public static readonly string[] Leaders = [PlatformOwner, Owner, ObjectAdmin];

    // Должности, которые выдаются приглашением внутри организации.
    public static readonly string[] Staff = [ObjectAdmin, Manager, Accountant, Content];
}
