using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIntentAndSpotlightTour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SpotlightTourCompleted",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserIntent",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpotlightTourCompleted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserIntent",
                table: "AspNetUsers");
        }
    }
}
