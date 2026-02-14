using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositDispute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisputeReason",
                table: "RentalDeposits",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputedAt",
                table: "RentalDeposits",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputeReason",
                table: "RentalDeposits");

            migrationBuilder.DropColumn(
                name: "DisputedAt",
                table: "RentalDeposits");
        }
    }
}
