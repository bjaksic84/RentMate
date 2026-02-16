using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class AddItemImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemImages_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemImages_ItemId_DisplayOrder",
                table: "ItemImages",
                columns: new[] { "ItemId", "DisplayOrder" });

            // Migrate existing ImageUrl values from Items to ItemImages
            migrationBuilder.Sql(@"
                INSERT INTO ""ItemImages"" (""ItemId"", ""ImageUrl"", ""DisplayOrder"", ""CreatedAt"")
                SELECT ""Id"", ""ImageUrl"", 0, COALESCE(""CreatedAt"", NOW())
                FROM ""Items""
                WHERE ""ImageUrl"" IS NOT NULL AND ""ImageUrl"" != ''
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemImages");
        }
    }
}
