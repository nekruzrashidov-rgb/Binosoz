using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentBlockNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "Apartments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Дом квартиры — по этажу (у прежних строк). Новые и перенесённые на другой этаж ставит приложение
            // (AppDbContext.SaveChanges), вставки seed.sql указывают его сами.
            migrationBuilder.Sql("""
                UPDATE "Apartments" a SET "BuildingId" = f."BuildingId" FROM "Floors" f WHERE f."Id" = a."FloorId";

                -- Прежние дома не перенумеровываются: если номер уже повторяется в доме, миграция останавливается с
                -- понятным текстом, ничего не меняя (номера правит сотрудник в CRM, затем миграцию запускают снова).
                DO $$
                DECLARE dup text;
                BEGIN
                    SELECT string_agg(DISTINCT b."NameRu" || ': №' || a."Number", ', ') INTO dup
                    FROM "Apartments" a JOIN "Buildings" b ON b."Id" = a."BuildingId"
                    WHERE (a."BuildingId", a."Number", CASE a."Kind" WHEN 'Penthouse' THEN 'Residential' ELSE a."Kind" END) IN (
                        SELECT "BuildingId", "Number", CASE "Kind" WHEN 'Penthouse' THEN 'Residential' ELSE "Kind" END
                        FROM "Apartments" GROUP BY 1, 2, 3 HAVING count(*) > 1);
                    IF dup IS NOT NULL THEN
                        RAISE EXCEPTION 'Номера помещений повторяются в пределах дома (%). Исправьте номера в CRM и запустите миграцию снова', dup;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_BuildingId_Number_Commercial",
                table: "Apartments",
                columns: new[] { "BuildingId", "Number" },
                unique: true,
                filter: "\"Kind\" = 'Commercial'");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_BuildingId_Number_Home",
                table: "Apartments",
                columns: new[] { "BuildingId", "Number" },
                unique: true,
                filter: "\"Kind\" IN ('Residential', 'Penthouse')");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_BuildingId_Number_Parking",
                table: "Apartments",
                columns: new[] { "BuildingId", "Number" },
                unique: true,
                filter: "\"Kind\" = 'Parking'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Apartments_BuildingId_Number_Commercial",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_BuildingId_Number_Home",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_BuildingId_Number_Parking",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "Apartments");
        }
    }
}
