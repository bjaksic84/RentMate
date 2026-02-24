using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class ClearLegacyImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear legacy ImageUrl on items that already have images in the ItemImages table.
            // The original AddItemImages migration copied ImageUrl into ItemImages,
            // but never cleared the source field — causing dual storage.
            migrationBuilder.Sql(@"
                UPDATE ""Items""
                SET ""ImageUrl"" = NULL
                WHERE ""ImageUrl"" IS NOT NULL
                  AND ""ImageUrl"" != ''
                  AND ""Id"" IN (SELECT DISTINCT ""ItemId"" FROM ""ItemImages"")
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore legacy ImageUrl from ItemImages (DisplayOrder = 0 is primary).
            migrationBuilder.Sql(@"
                UPDATE ""Items"" i
                SET ""ImageUrl"" = img.""ImageUrl""
                FROM ""ItemImages"" img
                WHERE img.""ItemId"" = i.""Id""
                  AND img.""DisplayOrder"" = 0
                  AND (i.""ImageUrl"" IS NULL OR i.""ImageUrl"" = '')
            ");
        }
    }
}
