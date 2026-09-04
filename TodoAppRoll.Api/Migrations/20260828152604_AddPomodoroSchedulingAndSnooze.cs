using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAppRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPomodoroSchedulingAndSnooze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaysOfWeek",
                table: "Alarms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SnoozeMinutes",
                table: "Alarms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WindowEnd",
                table: "Alarms",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WindowStart",
                table: "Alarms",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DaysOfWeek",
                table: "Alarms");

            migrationBuilder.DropColumn(
                name: "SnoozeMinutes",
                table: "Alarms");

            migrationBuilder.DropColumn(
                name: "WindowEnd",
                table: "Alarms");

            migrationBuilder.DropColumn(
                name: "WindowStart",
                table: "Alarms");
        }
    }
}
