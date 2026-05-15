using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Empty migration — exists only to sync the model snapshot with the current entity model.
    /// All schema changes are already applied by earlier migrations (AddUserPreferences,
    /// AddGdprFields, AddRentalArchivedAt, AddNotifications, AddUserIntentAndSpotlightTour, etc.)
    /// that were created in a separate working copy and copied into this project.
    /// </remarks>
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — schema already up to date via prior migrations.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty.
        }
    }
}
