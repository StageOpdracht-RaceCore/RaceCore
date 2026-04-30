using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerDraftFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jersey_Cyclists_CyclistId",
                table: "Jersey");

            migrationBuilder.DropForeignKey(
                name: "FK_Jersey_Stages_StageId",
                table: "Jersey");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Jersey",
                table: "Jersey");

            migrationBuilder.RenameTable(
                name: "Jersey",
                newName: "Jerseys");

            migrationBuilder.RenameIndex(
                name: "IX_Jersey_StageId",
                table: "Jerseys",
                newName: "IX_Jerseys_StageId");

            migrationBuilder.RenameIndex(
                name: "IX_Jersey_CyclistId",
                table: "Jerseys",
                newName: "IX_Jerseys_CyclistId");

            migrationBuilder.AddColumn<int>(
                name: "PositionInDraft",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPoints",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Jerseys",
                table: "Jerseys",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Jerseys_Cyclists_CyclistId",
                table: "Jerseys",
                column: "CyclistId",
                principalTable: "Cyclists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Jerseys_Stages_StageId",
                table: "Jerseys",
                column: "StageId",
                principalTable: "Stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jerseys_Cyclists_CyclistId",
                table: "Jerseys");

            migrationBuilder.DropForeignKey(
                name: "FK_Jerseys_Stages_StageId",
                table: "Jerseys");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Jerseys",
                table: "Jerseys");

            migrationBuilder.DropColumn(
                name: "PositionInDraft",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "TotalPoints",
                table: "Players");

            migrationBuilder.RenameTable(
                name: "Jerseys",
                newName: "Jersey");

            migrationBuilder.RenameIndex(
                name: "IX_Jerseys_StageId",
                table: "Jersey",
                newName: "IX_Jersey_StageId");

            migrationBuilder.RenameIndex(
                name: "IX_Jerseys_CyclistId",
                table: "Jersey",
                newName: "IX_Jersey_CyclistId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Jersey",
                table: "Jersey",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Jersey_Cyclists_CyclistId",
                table: "Jersey",
                column: "CyclistId",
                principalTable: "Cyclists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Jersey_Stages_StageId",
                table: "Jersey",
                column: "StageId",
                principalTable: "Stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
