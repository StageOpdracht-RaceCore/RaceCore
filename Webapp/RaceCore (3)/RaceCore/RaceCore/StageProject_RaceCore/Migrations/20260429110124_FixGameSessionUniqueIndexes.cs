using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    public partial class FixGameSessionUniqueIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_DraftTurns_RaceId ON DraftTurns (RaceId);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_PlayerSelections_PlayerId ON PlayerSelections (PlayerId);");

            migrationBuilder.Sql("ALTER TABLE DraftTurns DROP INDEX IX_DraftTurns_RaceId_TurnNumber;");
            migrationBuilder.Sql("ALTER TABLE PlayerSelections DROP INDEX IX_PlayerSelections_PlayerId_RaceId_CyclistId;");

            migrationBuilder.CreateIndex(
                name: "IX_DraftTurns_GameSessionId_TurnNumber",
                table: "DraftTurns",
                columns: new[] { "GameSessionId", "TurnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSelections_GameSessionId_CyclistId",
                table: "PlayerSelections",
                columns: new[] { "GameSessionId", "CyclistId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DraftTurns_GameSessionId_TurnNumber",
                table: "DraftTurns");

            migrationBuilder.DropIndex(
                name: "IX_PlayerSelections_GameSessionId_CyclistId",
                table: "PlayerSelections");

            migrationBuilder.CreateIndex(
                name: "IX_DraftTurns_RaceId_TurnNumber",
                table: "DraftTurns",
                columns: new[] { "RaceId", "TurnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSelections_PlayerId_RaceId_CyclistId",
                table: "PlayerSelections",
                columns: new[] { "PlayerId", "RaceId", "CyclistId" },
                unique: true);
        }
    }
}