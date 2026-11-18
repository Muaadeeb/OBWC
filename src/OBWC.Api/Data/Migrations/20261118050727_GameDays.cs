using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBWC.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GameDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GameDayId",
                table: "PlaySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Venue = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    OnlineLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LocationNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsCancelled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CancelledUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SeriesKey = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameDays_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_GameDayId",
                table: "PlaySessions",
                column: "GameDayId");

            migrationBuilder.CreateIndex(
                name: "IX_GameDays_CreatedByUserId",
                table: "GameDays",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameDays_ScheduledAt",
                table: "GameDays",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameDays_SeriesKey",
                table: "GameDays",
                column: "SeriesKey");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_GameDays_GameDayId",
                table: "PlaySessions",
                column: "GameDayId",
                principalTable: "GameDays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("DELETE FROM SessionAttendances WHERE RsvpStatus = 'Unknown' AND Status <> 'Present';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_GameDays_GameDayId",
                table: "PlaySessions");

            migrationBuilder.DropTable(
                name: "GameDays");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_GameDayId",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "GameDayId",
                table: "PlaySessions");
        }
    }
}
