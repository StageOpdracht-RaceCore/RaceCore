using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    public partial class AddGameSessionToStageResultsAndJerseys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StageResults_StageId",
                table: "StageResults",
                column: "StageId");

            migrationBuilder.DropIndex(
                name: "IX_StageResults_StageId_CyclistId",
                table: "StageResults");

            migrationBuilder.AddColumn<int>(
                name: "GameSessionId",
                table: "StageResults",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Jerseys",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "GameSessionId",
                table: "Jerseys",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE `StageResults` sr
                JOIN `Stages` s ON s.`Id` = sr.`StageId`
                JOIN (
                    SELECT `RaceId`, MAX(`Id`) AS `GameSessionId`
                    FROM `GameSessions`
                    GROUP BY `RaceId`
                ) gs ON gs.`RaceId` = s.`RaceId`
                SET sr.`GameSessionId` = gs.`GameSessionId`;
            ");

            migrationBuilder.Sql(@"
                UPDATE `Jerseys` j
                JOIN `Stages` s ON s.`Id` = j.`StageId`
                JOIN (
                    SELECT `RaceId`, MAX(`Id`) AS `GameSessionId`
                    FROM `GameSessions`
                    GROUP BY `RaceId`
                ) gs ON gs.`RaceId` = s.`RaceId`
                SET j.`GameSessionId` = gs.`GameSessionId`;
            ");

            migrationBuilder.Sql("DELETE FROM `StageResults` WHERE `GameSessionId` IS NULL;");
            migrationBuilder.Sql("DELETE FROM `Jerseys` WHERE `GameSessionId` IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "GameSessionId",
                table: "StageResults",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GameSessionId",
                table: "Jerseys",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_GameSessionId_StageId_CyclistId",
                table: "StageResults",
                columns: new[] { "GameSessionId", "StageId", "CyclistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jerseys_GameSessionId_StageId_Type",
                table: "Jerseys",
                columns: new[] { "GameSessionId", "StageId", "Type" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Jerseys_GameSessions_GameSessionId",
                table: "Jerseys",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StageResults_GameSessions_GameSessionId",
                table: "StageResults",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jerseys_GameSessions_GameSessionId",
                table: "Jerseys");

            migrationBuilder.DropForeignKey(
                name: "FK_StageResults_GameSessions_GameSessionId",
                table: "StageResults");

            migrationBuilder.DropIndex(
                name: "IX_StageResults_GameSessionId_StageId_CyclistId",
                table: "StageResults");

            migrationBuilder.DropIndex(
                name: "IX_StageResults_StageId",
                table: "StageResults");

            migrationBuilder.DropIndex(
                name: "IX_Jerseys_GameSessionId_StageId_Type",
                table: "Jerseys");

            migrationBuilder.DropColumn(
                name: "GameSessionId",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "GameSessionId",
                table: "Jerseys");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Jerseys",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_StageId_CyclistId",
                table: "StageResults",
                columns: new[] { "StageId", "CyclistId" },
                unique: true);
        }
    }
}