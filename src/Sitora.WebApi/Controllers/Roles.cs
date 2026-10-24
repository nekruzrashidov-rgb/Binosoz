using Sitora.Domain.Enums;

namespace Sitora.WebApi.Controllers;

// Наборы ролей для [Authorize(Roles = ...)]. Admin может всё, поэтому входит в каждый набор.
public static class Roles
{
    public const string Admin = RoleNames.Admin;
    public const string CatalogEditors = RoleNames.Content + "," + RoleNames.Admin;
    public const string Sales = RoleNames.Manager + "," + RoleNames.Admin;
}
