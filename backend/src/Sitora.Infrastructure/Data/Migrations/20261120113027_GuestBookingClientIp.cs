using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuestBookingClientIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientIp",
                table: "Bookings",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClientIp",
                table: "Bookings",
                column: "ClientIp",
                filter: "\"Status\" = 'Active' AND \"ClientIp\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_ClientIp",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ClientIp",
                table: "Bookings");
        }
    }
}
