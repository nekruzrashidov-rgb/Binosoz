using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentPlanSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model3dId",
                table: "Apartments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanSlot",
                table: "Apartments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Уже существующим квартирам позиция назначается по порядку номеров на этаже (0, 1, 2…).
            // Если на каком-то этаже больше 8 квартир, проверка 0–7 ниже остановит миграцию:
            // такие этажи нужно разметить вручную до обновления.
            migrationBuilder.Sql("""
                UPDATE "Apartments" AS a
                SET "PlanSlot" = s.slot
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "FloorId" ORDER BY "Number") - 1 AS slot
                    FROM "Apartments"
                ) AS s
                WHERE a."Id" = s."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_FloorId_PlanSlot",
                table: "Apartments",
                columns: new[] { "FloorId", "PlanSlot" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Apartments_PlanSlot",
                table: "Apartments",
                sql: "\"PlanSlot\" BETWEEN 0 AND 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Apartments_FloorId_PlanSlot",
                table: "Apartments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Apartments_PlanSlot",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Model3dId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "PlanSlot",
                table: "Apartments");
        }
    }
}
