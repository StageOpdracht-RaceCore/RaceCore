using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessionToPlayerPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameSessionId",
                table: "PlayerPoints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoints_GameSessionId",
                table: "PlayerPoints",
                column: "GameSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerPoints_GameSessions_GameSessionId",
                table: "PlayerPoints",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerPoints_GameSessions_GameSessionId",
                table: "PlayerPoints");

            migrationBuilder.DropIndex(
                name: "IX_PlayerPoints_GameSessionId",
                table: "PlayerPoints");

            migrationBuilder.DropColumn(
                name: "GameSessionId",
                table: "PlayerPoints");
        }
    }
}
