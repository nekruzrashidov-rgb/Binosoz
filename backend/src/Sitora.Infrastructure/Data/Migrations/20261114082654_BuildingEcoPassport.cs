using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuildingEcoPassport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EcoEnergyClass",
                table: "Buildings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EcoEstimatedKwhPerM2Year",
                table: "Buildings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EcoGreenAreaM2",
                table: "Buildings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcoHasEvCharging",
                table: "Buildings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcoHasHeatMeters",
                table: "Buildings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcoHasLedCommonAreas",
                table: "Buildings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcoHasRainwaterReuse",
                table: "Buildings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcoHasWasteSorting",
                table: "Buildings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcoHasWaterMeters",
                table: "Buildings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoHeatingType",
                table: "Buildings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EcoSolarPanelsKw",
                table: "Buildings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWallInsulationRu",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWallInsulationTg",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWindowsRu",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWindowsTg",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EcoEnergyClass",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoEstimatedKwhPerM2Year",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoGreenAreaM2",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHasEvCharging",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHasHeatMeters",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHasLedCommonAreas",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHasRainwaterReuse",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHasWasteSorting",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHasWaterMeters",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoHeatingType",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoSolarPanelsKw",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoWallInsulationRu",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoWallInsulationTg",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoWindowsRu",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "EcoWindowsTg",
                table: "Buildings");
        }
    }
}
