using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Organizations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Organizations");
        }
    }
}
