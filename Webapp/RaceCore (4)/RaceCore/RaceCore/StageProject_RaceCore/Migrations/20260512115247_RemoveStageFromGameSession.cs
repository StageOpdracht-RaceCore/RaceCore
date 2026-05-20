using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStageFromGameSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameSessions_Stages_StageId",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_StageId",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "GameSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StageId",
                table: "GameSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_StageId",
                table: "GameSessions",
                column: "StageId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameSessions_Stages_StageId",
                table: "GameSessions",
                column: "StageId",
                principalTable: "Stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
