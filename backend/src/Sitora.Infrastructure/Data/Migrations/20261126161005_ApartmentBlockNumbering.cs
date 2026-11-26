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

            // Дом квартиры — по этажу: у прежних строк сразу, у новых и перенесённых на другой этаж — триггером
            // (EF и seed.sql вставляют квартиры без BuildingId).
            migrationBuilder.Sql("""
                UPDATE "Apartments" a SET "BuildingId" = f."BuildingId" FROM "Floors" f WHERE f."Id" = a."FloorId";

                CREATE OR REPLACE FUNCTION apartments_set_building() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    SELECT f."BuildingId" INTO NEW."BuildingId" FROM "Floors" f WHERE f."Id" = NEW."FloorId";
                    RETURN NEW;
                END $$;

                CREATE TRIGGER trg_apartments_building BEFORE INSERT OR UPDATE OF "FloorId", "BuildingId" ON "Apartments"
                    FOR EACH ROW EXECUTE FUNCTION apartments_set_building();

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
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_apartments_building ON "Apartments";
                DROP FUNCTION IF EXISTS apartments_set_building();
                """);

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
