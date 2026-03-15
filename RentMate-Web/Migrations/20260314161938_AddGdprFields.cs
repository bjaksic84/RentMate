using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentMate.Migrations
{
    /// <inheritdoc />
    public partial class AddGdprFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivatedBy",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeactivated",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyPolicyAcceptedAt",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyPolicyVersion",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CookieConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    NecessaryCookies = table.Column<bool>(type: "boolean", nullable: false),
                    AnalyticsCookies = table.Column<bool>(type: "boolean", nullable: false),
                    MarketingCookies = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddressHash = table.Column<string>(type: "text", nullable: true),
                    ConsentedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookieConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookieConsents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CookieConsents_ConsentedAt",
                table: "CookieConsents",
                column: "ConsentedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CookieConsents_UserId",
                table: "CookieConsents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CookieConsents");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeactivatedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsDeactivated",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyAcceptedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyVersion",
                table: "AspNetUsers");
        }
    }
}
