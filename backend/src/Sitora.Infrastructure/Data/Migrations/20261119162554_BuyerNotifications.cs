using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuyerNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Уже зарегистрированные: оба канала и все события включены (31 = NotifyEventGroups.All).
            migrationBuilder.AddColumn<bool>(
                name: "NotifyByEmail",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyByTelegram",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "NotifyEvents",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 31);

            migrationBuilder.AddColumn<string>(
                name: "NotifyLanguage",
                table: "Users",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuyerMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Event = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Html = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DedupeKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyerMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuyerMessages_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuyerMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramChatId",
                table: "Users",
                column: "TelegramChatId",
                filter: "\"TelegramChatId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BuyerMessages_DealId_CreatedAt",
                table: "BuyerMessages",
                columns: new[] { "DealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BuyerMessages_DedupeKey",
                table: "BuyerMessages",
                column: "DedupeKey",
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BuyerMessages_OrganizationId_CreatedAt",
                table: "BuyerMessages",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BuyerMessages_UserId",
                table: "BuyerMessages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuyerMessages");

            migrationBuilder.DropIndex(
                name: "IX_Users_TelegramChatId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyByEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyByTelegram",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyEvents",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyLanguage",
                table: "Users");
        }
    }
}
