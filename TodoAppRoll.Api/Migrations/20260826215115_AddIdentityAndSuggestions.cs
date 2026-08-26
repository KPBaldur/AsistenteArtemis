using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAppRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "AISmartSuggestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssistantIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Tone = table.Column<string>(type: "text", nullable: false),
                    StyleRules = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantIdentities_UserId",
                table: "AssistantIdentities",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantIdentities");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "AISmartSuggestions");
        }
    }
}
