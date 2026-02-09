using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositsExtensionsAccessories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoApproveExtensions",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Items",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRentalDays",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemAccessories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DailyPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAccessories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemAccessories_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RentalId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ChargedAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    ChargeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AuthorizedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ChargedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalDeposits_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalExtensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RentalId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    OriginalEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NewEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AdditionalCost = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalExtensions_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalExtensions_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalAccessories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RentalId = table.Column<int>(type: "integer", nullable: false),
                    ItemAccessoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DailyPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalAccessories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalAccessories_ItemAccessories_ItemAccessoryId",
                        column: x => x.ItemAccessoryId,
                        principalTable: "ItemAccessories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalAccessories_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemAccessories_ItemId",
                table: "ItemAccessories",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalAccessories_ItemAccessoryId",
                table: "RentalAccessories",
                column: "ItemAccessoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalAccessories_RentalId",
                table: "RentalAccessories",
                column: "RentalId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalDeposits_RentalId",
                table: "RentalDeposits",
                column: "RentalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalExtensions_RentalId_Status",
                table: "RentalExtensions",
                columns: new[] { "RentalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalExtensions_RequestedByUserId",
                table: "RentalExtensions",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RentalAccessories");

            migrationBuilder.DropTable(
                name: "RentalDeposits");

            migrationBuilder.DropTable(
                name: "RentalExtensions");

            migrationBuilder.DropTable(
                name: "ItemAccessories");

            migrationBuilder.DropColumn(
                name: "AutoApproveExtensions",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MaxRentalDays",
                table: "Items");
        }
    }
}
