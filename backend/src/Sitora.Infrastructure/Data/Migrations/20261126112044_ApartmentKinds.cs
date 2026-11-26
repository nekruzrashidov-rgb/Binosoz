using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Apartments_PlanSlot",
                table: "Apartments");

            migrationBuilder.AddColumn<decimal>(
                name: "CommercialPricePerM2",
                table: "Buildings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ElevatorsCount",
                table: "Buildings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParkingSpotPrice",
                table: "Buildings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Apartments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Residential");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Apartments_PlanSlot",
                table: "Apartments",
                sql: "\"PlanSlot\" BETWEEN 0 AND 99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Apartments_PlanSlot",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "CommercialPricePerM2",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "ElevatorsCount",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "ParkingSpotPrice",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Apartments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Apartments_PlanSlot",
                table: "Apartments",
                sql: "\"PlanSlot\" BETWEEN 0 AND 7");
        }
    }
}
