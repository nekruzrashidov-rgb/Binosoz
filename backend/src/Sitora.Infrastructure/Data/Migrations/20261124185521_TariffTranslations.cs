using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TariffTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeaturesEn",
                table: "TariffPlans",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "FeaturesTg",
                table: "TariffPlans",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "TariffPlans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameTg",
                table: "TariffPlans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeaturesEn",
                table: "TariffPlans");

            migrationBuilder.DropColumn(
                name: "FeaturesTg",
                table: "TariffPlans");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "TariffPlans");

            migrationBuilder.DropColumn(
                name: "NameTg",
                table: "TariffPlans");
        }
    }
}
