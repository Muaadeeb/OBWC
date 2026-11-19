using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBWC.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleVenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "PlaySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "GameDays",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_VenueId",
                table: "PlaySessions",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_GameDays_VenueId",
                table: "GameDays",
                column: "VenueId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameDays_Venues_VenueId",
                table: "GameDays",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Venues_VenueId",
                table: "PlaySessions",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameDays_Venues_VenueId",
                table: "GameDays");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Venues_VenueId",
                table: "PlaySessions");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_VenueId",
                table: "PlaySessions");

            migrationBuilder.DropIndex(
                name: "IX_GameDays_VenueId",
                table: "GameDays");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "GameDays");
        }
    }
}
