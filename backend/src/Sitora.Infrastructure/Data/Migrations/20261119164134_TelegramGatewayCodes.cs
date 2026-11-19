using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TelegramGatewayCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "SmsCodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "sms");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "SmsCodes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayRequestId",
                table: "SmsCodes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestIp",
                table: "SmsCodes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsCodes_GatewayRequestId",
                table: "SmsCodes",
                column: "GatewayRequestId",
                filter: "\"GatewayRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SmsCodes_RequestIp_CreatedAt",
                table: "SmsCodes",
                columns: new[] { "RequestIp", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsCodes_GatewayRequestId",
                table: "SmsCodes");

            migrationBuilder.DropIndex(
                name: "IX_SmsCodes_RequestIp_CreatedAt",
                table: "SmsCodes");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "SmsCodes");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "SmsCodes");

            migrationBuilder.DropColumn(
                name: "GatewayRequestId",
                table: "SmsCodes");

            migrationBuilder.DropColumn(
                name: "RequestIp",
                table: "SmsCodes");
        }
    }
}
