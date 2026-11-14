using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AmenitiesAndInstallment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InstallmentDownPaymentMinPct",
                table: "Organizations",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentMaxMonths",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstallmentNoOverpayment",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "Amenities",
                table: "Buildings",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallmentDownPaymentMinPct",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "InstallmentMaxMonths",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "InstallmentNoOverpayment",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "Buildings");
        }
    }
}
