using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuildingBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SitePlan",
                table: "Organizations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockCode",
                table: "Buildings",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Footprint",
                table: "Buildings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Setbacks",
                table: "Buildings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SitePosition",
                table: "Buildings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Buildings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SitePlan",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BlockCode",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "Footprint",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "Setbacks",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "SitePosition",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Buildings");
        }
    }
}
