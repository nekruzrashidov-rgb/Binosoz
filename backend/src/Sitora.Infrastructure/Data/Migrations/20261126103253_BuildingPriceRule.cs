using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuildingPriceRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PenthousePricePerM2",
                table: "Buildings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceCurrency",
                table: "Buildings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TJS");

            migrationBuilder.AddColumn<string>(
                name: "PriceSteps",
                table: "Buildings",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenthousePricePerM2",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PriceCurrency",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PriceSteps",
                table: "Buildings");
        }
    }
}
