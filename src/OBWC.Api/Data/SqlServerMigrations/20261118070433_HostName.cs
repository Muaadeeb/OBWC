using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBWC.Api.Data.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class HostName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostName",
                table: "PlaySessions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostName",
                table: "PlaySessions");
        }
    }
}
