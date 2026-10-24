using Sitora.Domain.Enums;

namespace Sitora.WebApi.Controllers;

// Наборы ролей для [Authorize(Roles = ...)].
public static class Roles
{
    public const string CatalogEditors = RoleNames.Content + "," + RoleNames.Admin;
}
