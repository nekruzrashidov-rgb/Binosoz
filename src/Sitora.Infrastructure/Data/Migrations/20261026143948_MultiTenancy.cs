using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Sitora.Domain.Entities;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_Currency_Date",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_UserId",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "Leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Leads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ExchangeRates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HomeBuildingId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Buildings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ImpersonatedById",
                table: "AuditLog",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AuditLog",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Sent = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            // Перенос текущих данных: всё, что было до мультиарендности, принадлежит одной организации по умолчанию.
            migrationBuilder.Sql($"""
                INSERT INTO "Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES ('{Organization.DefaultId}', 'Sitora', '{Organization.DefaultSlug}', TRUE, now(), now());

                UPDATE "Buildings" SET "OrganizationId" = '{Organization.DefaultId}';
                UPDATE "Customers" SET "OrganizationId" = '{Organization.DefaultId}';
                UPDATE "Leads" SET "OrganizationId" = '{Organization.DefaultId}';
                UPDATE "AuditLog" SET "OrganizationId" = '{Organization.DefaultId}';

                -- Дом заявки — дом её квартиры.
                UPDATE "Leads" AS l SET "BuildingId" = f."BuildingId"
                FROM "Apartments" AS a JOIN "Floors" AS f ON f."Id" = a."FloorId"
                WHERE l."ApartmentId" = a."Id";

                -- Почта покупателя — ключ входа в кабинет: храним в одном виде.
                UPDATE "Customers" SET "Email" = lower(btrim("Email")) WHERE "Email" IS NOT NULL;
                UPDATE "Customers" SET "Email" = NULL WHERE "Email" = '';

                -- Администратор становится владельцем организации; назначения ролей пользователям сохраняются.
                UPDATE "Roles" SET "Name" = 'Owner', "NormalizedName" = 'OWNER' WHERE "Name" = 'Admin';

                -- Все сотрудники — в организации по умолчанию, с доступом ко всем её домам.
                UPDATE "Users" AS u SET "OrganizationId" = '{Organization.DefaultId}'
                WHERE EXISTS (
                    SELECT 1 FROM "UserRoles" AS ur JOIN "Roles" AS r ON r."Id" = ur."RoleId"
                    WHERE ur."UserId" = u."Id" AND r."Name" IN ('Owner', 'Manager', 'Accountant', 'Content'));
                """);

            migrationBuilder.CreateTable(
                name: "UserBuildings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBuildings", x => new { x.UserId, x.BuildingId });
                    table.ForeignKey(
                        name: "FK_UserBuildings_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBuildings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BuildingIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId",
                table: "Users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BuildingId",
                table: "Leads",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OrganizationId_BuildingId",
                table: "Leads",
                columns: new[] { "OrganizationId", "BuildingId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_OrganizationId_Currency_Date",
                table: "ExchangeRates",
                columns: new[] { "OrganizationId", "Currency", "Date" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_HomeBuildingId",
                table: "Customers",
                column: "HomeBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId_Phone",
                table: "Customers",
                columns: new[] { "OrganizationId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId",
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_OrganizationId",
                table: "Buildings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OrganizationId_At",
                table: "AuditLog",
                columns: new[] { "OrganizationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_Email_CreatedAt",
                table: "EmailCodes",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_RequestIp_CreatedAt",
                table: "EmailCodes",
                columns: new[] { "RequestIp", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OrganizationId_Email",
                table: "Invitations",
                columns: new[] { "OrganizationId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBuildings_BuildingId",
                table: "UserBuildings",
                column: "BuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Organizations_OrganizationId",
                table: "Buildings",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Buildings_HomeBuildingId",
                table: "Customers",
                column: "HomeBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Organizations_OrganizationId",
                table: "Customers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_Organizations_OrganizationId",
                table: "ExchangeRates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Buildings_BuildingId",
                table: "Leads",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Organizations_OrganizationId",
                table: "Leads",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Roles" SET "Name" = 'Admin', "NormalizedName" = 'ADMIN' WHERE "Name" = 'Owner';""");

            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Organizations_OrganizationId",
                table: "Buildings");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Buildings_HomeBuildingId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Organizations_OrganizationId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_Organizations_OrganizationId",
                table: "ExchangeRates");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Buildings_BuildingId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Organizations_OrganizationId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "EmailCodes");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "UserBuildings");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Leads_BuildingId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_OrganizationId_BuildingId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_OrganizationId_Currency_Date",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_HomeBuildingId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_OrganizationId_Phone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_UserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_OrganizationId",
                table: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_OrganizationId_At",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "HomeBuildingId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "ImpersonatedById",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuditLog");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_Currency_Date",
                table: "ExchangeRates",
                columns: new[] { "Currency", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");
        }
    }
}
