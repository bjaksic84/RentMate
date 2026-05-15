using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class FixDisputedRentalArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix: un-archive rentals that have active deposit disputes.
            // The original AddRentalArchivedAt migration incorrectly archived all
            // completed/cancelled rentals, including those with active disputes.
            migrationBuilder.Sql("""
                UPDATE "Rentals"
                SET "ArchivedAt" = NULL
                WHERE "Id" IN (
                    SELECT r."Id" FROM "Rentals" r
                    INNER JOIN "RentalDeposits" d ON d."RentalId" = r."Id"
                    WHERE d."Status" IN ('Disputed', 'CounterOffered', 'Escalated')
                )
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
