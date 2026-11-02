using Sitora.Domain.Enums;

namespace Sitora.WebApi.Controllers;

// Наборы ролей для [Authorize(Roles = ...)]. Роль открывает раздел, а какие именно данные видны —
// решают фильтры организации и домов в AppDbContext (сотрудник видит только своё).
public static class Roles
{
    public const string SuperAdmin = RoleNames.SuperAdmin;
    public const string Customer = RoleNames.Customer;

    // Руководители: создатель платформы, владелец (вся организация), админ объекта (один дом).
    public const string Leaders = RoleNames.SuperAdmin + "," + RoleNames.Owner + "," + RoleNames.ObjectAdmin;

    // Создают дома и смотрят журнал всей организации.
    public const string OrganizationLeaders = RoleNames.SuperAdmin + "," + RoleNames.Owner;

    public const string CatalogEditors = RoleNames.Content + "," + Leaders;
    public const string Sales = RoleNames.Manager + "," + Leaders;
    public const string Accounting = RoleNames.Accountant + "," + Leaders;
    public const string DealStaff = RoleNames.Manager + "," + RoleNames.Accountant + "," + Leaders;

    // Владелец сделки или сотрудник; что сделка своя, проверяет сервис (чужая — 404).
    public const string DealViewers = RoleNames.Customer + "," + DealStaff;

    // Любой сотрудник (все роли, кроме покупателя): вход по паролю и настройка 2FA.
    public const string AllStaff = RoleNames.Content + "," + DealStaff;
}
