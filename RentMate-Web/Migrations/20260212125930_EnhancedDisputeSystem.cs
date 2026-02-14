using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class EnhancedDisputeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "RentalDeposits",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminResolvedAt",
                table: "RentalDeposits",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminResolvedByUserId",
                table: "RentalDeposits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CounterOfferAmount",
                table: "RentalDeposits",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounterOfferAt",
                table: "RentalDeposits",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeDeadline",
                table: "RentalDeposits",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "RentalDeposits",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerDisputeResponse",
                table: "RentalDeposits",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "AdminResolvedAt",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "AdminResolvedByUserId",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "CounterOfferAmount",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "CounterOfferAt",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "DisputeDeadline",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "OwnerDisputeResponse",
                table: "RentalDeposits");
        }
    }
}
