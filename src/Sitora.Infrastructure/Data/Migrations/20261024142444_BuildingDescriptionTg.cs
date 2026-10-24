using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    // Исправлено вручную: EF предложил переименовать Description в DescriptionTg,
    // но в старом поле русский текст, поэтому оно становится DescriptionRu.
    public partial class BuildingDescriptionTg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Buildings",
                newName: "DescriptionRu");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionTg",
                table: "Buildings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionTg",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "DescriptionRu",
                table: "Buildings",
                newName: "Description");
        }
    }
}
