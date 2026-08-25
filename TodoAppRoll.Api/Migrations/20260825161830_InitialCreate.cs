using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAppRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIChatSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AISmartSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestionType = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AISmartSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AISmartSuggestions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityName = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    FileType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AIMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIMessages_AIChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AIChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alarms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    ScheduleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AudioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alarms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alarms_MediaFiles_AudioId",
                        column: x => x.AudioId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Alarms_MediaFiles_ImageId",
                        column: x => x.ImageId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Alarms_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AlarmId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Alarms_AlarmId",
                        column: x => x.AlarmId,
                        principalTable: "Alarms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tasks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subtasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subtasks_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIChatSessions_UserId",
                table: "AIChatSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIMessages_SessionId",
                table: "AIMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AISmartSuggestions_UserId",
                table: "AISmartSuggestions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_AudioId",
                table: "Alarms",
                column: "AudioId");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_ImageId",
                table: "Alarms",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_UserId",
                table: "Alarms",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_UserId",
                table: "MediaFiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subtasks_TaskId",
                table: "Subtasks",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AlarmId",
                table: "Tasks",
                column: "AlarmId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UserId",
                table: "Tasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIMessages");

            migrationBuilder.DropTable(
                name: "AISmartSuggestions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Subtasks");

            migrationBuilder.DropTable(
                name: "AIChatSessions");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "Alarms");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
