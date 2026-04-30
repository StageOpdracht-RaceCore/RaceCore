using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    public partial class AddGameSession : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM DraftTurns;");
            migrationBuilder.Sql("DELETE FROM PlayerSelections;");

            migrationBuilder.AddColumn<int>(
                name: "GameSessionId",
                table: "PlayerSelections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GameSessionId",
                table: "DraftTurns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RaceId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentStageNumber = table.Column<int>(type: "int", nullable: false),
                    RidersPerPlayer = table.Column<int>(type: "int", nullable: false),
                    BenchPerPlayer = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSelections_GameSessionId",
                table: "PlayerSelections",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftTurns_GameSessionId",
                table: "DraftTurns",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_RaceId",
                table: "GameSessions",
                column: "RaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DraftTurns_GameSessions_GameSessionId",
                table: "DraftTurns",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerSelections_GameSessions_GameSessionId",
                table: "PlayerSelections",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DraftTurns_GameSessions_GameSessionId",
                table: "DraftTurns");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerSelections_GameSessions_GameSessionId",
                table: "PlayerSelections");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_PlayerSelections_GameSessionId",
                table: "PlayerSelections");

            migrationBuilder.DropIndex(
                name: "IX_DraftTurns_GameSessionId",
                table: "DraftTurns");

            migrationBuilder.DropColumn(
                name: "GameSessionId",
                table: "PlayerSelections");

            migrationBuilder.DropColumn(
                name: "GameSessionId",
                table: "DraftTurns");
        }
    }
}