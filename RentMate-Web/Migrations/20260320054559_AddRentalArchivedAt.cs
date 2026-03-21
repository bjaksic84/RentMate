using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalArchivedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Rentals",
                type: "timestamp without time zone",
                nullable: true);

            // Retroactively archive existing completed/cancelled rentals so they don't flood active tabs
            // Exclude rentals with active deposit disputes (they must stay in active tabs)
            migrationBuilder.Sql("""
                UPDATE "Rentals"
                SET "ArchivedAt" = COALESCE("UpdatedAt", "CreatedAt")
                WHERE "Status" IN ('Completed', 'Cancelled')
                AND "Id" NOT IN (
                    SELECT r."Id" FROM "Rentals" r
                    INNER JOIN "RentalDeposits" d ON d."RentalId" = r."Id"
                    WHERE d."Status" IN ('Disputed', 'CounterOffered', 'Escalated')
                )
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Rentals");
        }
    }
}
