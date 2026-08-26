using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAppRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Monitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastStatus = table.Column<int>(type: "integer", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastResponseTimeMs = table.Column<int>(type: "integer", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Monitors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitorCheckResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitorCheckResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitorCheckResults_Monitors_MonitorId",
                        column: x => x.MonitorId,
                        principalTable: "Monitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitorCheckResults_MonitorId",
                table: "MonitorCheckResults",
                column: "MonitorId");

            migrationBuilder.CreateIndex(
                name: "IX_Monitors_UserId",
                table: "Monitors",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitorCheckResults");

            migrationBuilder.DropTable(
                name: "Monitors");
        }
    }
}
