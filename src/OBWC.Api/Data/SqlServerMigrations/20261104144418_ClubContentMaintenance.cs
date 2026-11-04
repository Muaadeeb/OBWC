using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBWC.Api.Data.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class ClubContentMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhenText",
                table: "Conventions");

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "Officers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Officers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Officers",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "GamingGroups",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameFocus",
                table: "GamingGroups",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndsOn",
                table: "Conventions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartsOn",
                table: "Conventions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "ClubDocuments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MembersOnly");

            migrationBuilder.CreateTable(
                name: "EventPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventPhotos_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventPhotos_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventPhotos_OwnerUserId",
                table: "EventPhotos",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPhotos_PlaySessionId",
                table: "EventPhotos",
                column: "PlaySessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventPhotos");

            migrationBuilder.DropColumn(
                name: "Contact",
                table: "Officers");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Officers");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Officers");

            migrationBuilder.DropColumn(
                name: "Contact",
                table: "GamingGroups");

            migrationBuilder.DropColumn(
                name: "GameFocus",
                table: "GamingGroups");

            migrationBuilder.DropColumn(
                name: "EndsOn",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "StartsOn",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "ClubDocuments");

            migrationBuilder.AddColumn<string>(
                name: "WhenText",
                table: "Conventions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
