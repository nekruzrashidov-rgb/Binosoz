using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GreenSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GreenBaselineKwhPerM2Year",
                table: "Organizations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GreenBaselineSource",
                table: "Organizations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GreenCo2KgPerKgPaper",
                table: "Organizations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GreenCo2Source",
                table: "Organizations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GreenIsDemo",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GreenTariffKwhTjs",
                table: "Organizations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GreenBaselineKwhPerM2Year",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "GreenBaselineSource",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "GreenCo2KgPerKgPaper",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "GreenCo2Source",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "GreenIsDemo",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "GreenTariffKwhTjs",
                table: "Organizations");
        }
    }
}
