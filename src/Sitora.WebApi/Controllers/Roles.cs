using Sitora.Domain.Enums;

namespace Sitora.WebApi.Controllers;

// Наборы ролей для [Authorize(Roles = ...)]. Admin может всё, поэтому входит в каждый набор.
public static class Roles
{
    public const string Admin = RoleNames.Admin;
    public const string Customer = RoleNames.Customer;
    public const string CatalogEditors = RoleNames.Content + "," + RoleNames.Admin;
    public const string Sales = RoleNames.Manager + "," + RoleNames.Admin;
    public const string Accounting = RoleNames.Accountant + "," + RoleNames.Admin;
    public const string DealStaff = RoleNames.Manager + "," + RoleNames.Accountant + "," + RoleNames.Admin;

    // Владелец сделки или сотрудник; что сделка своя, проверяет сервис (чужая — 404).
    public const string DealViewers = RoleNames.Customer + "," + DealStaff;
}
