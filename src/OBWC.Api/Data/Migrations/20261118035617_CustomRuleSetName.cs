using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBWC.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomRuleSetName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomRuleSetName",
                table: "PlaySessions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomRuleSetName",
                table: "PlaySessions");
        }
    }
}
