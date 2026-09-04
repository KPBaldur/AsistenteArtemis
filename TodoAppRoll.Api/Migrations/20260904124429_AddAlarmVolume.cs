using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAppRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlarmVolume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Volume",
                table: "Alarms",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Volume",
                table: "Alarms");
        }
    }
}
