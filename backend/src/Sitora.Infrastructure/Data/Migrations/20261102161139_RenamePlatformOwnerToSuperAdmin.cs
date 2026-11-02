using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Роль создателя платформы переименована: PlatformOwner → SuperAdmin. Строка роли та же (Id не меняется),
    /// поэтому у пользователей роль сохраняется; в выданных ранее токенах старое имя — до следующего входа или обновления.
    /// </summary>
    public partial class RenamePlatformOwnerToSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Roles" SET "Name" = 'SuperAdmin', "NormalizedName" = 'SUPERADMIN'
                WHERE "NormalizedName" = 'PLATFORMOWNER';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Roles" SET "Name" = 'PlatformOwner', "NormalizedName" = 'PLATFORMOWNER'
                WHERE "NormalizedName" = 'SUPERADMIN';
                """);
        }
    }
}
