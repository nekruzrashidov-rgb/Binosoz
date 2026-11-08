using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    // Дом застройщика по умолчанию теперь «Некруз Резиденс»: его короткое имя (по нему сайт находит дома) — nekruz вместо sitora.
    // Значения записаны строками, а не через Organization.DefaultSlug: миграция не должна меняться вместе с константой.
    // Если имя nekruz уже занято другой организацией, переименование пропускается (короткое имя уникально).
    /// <inheritdoc />
    public partial class RenameDefaultOrganizationSlug : Migration
    {
        private const string DefaultId = "01a20000-0000-7000-8000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "Organizations" SET "Slug" = 'nekruz', "UpdatedAt" = now()
                WHERE "Id" = '{DefaultId}' AND "Slug" = 'sitora'
                  AND NOT EXISTS (SELECT 1 FROM "Organizations" WHERE "Slug" = 'nekruz');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "Organizations" SET "Slug" = 'sitora', "UpdatedAt" = now()
                WHERE "Id" = '{DefaultId}' AND "Slug" = 'nekruz'
                  AND NOT EXISTS (SELECT 1 FROM "Organizations" WHERE "Slug" = 'sitora');
                """);
        }
    }
}
