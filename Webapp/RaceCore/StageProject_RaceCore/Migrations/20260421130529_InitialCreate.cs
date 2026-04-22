using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointsRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FromPosition = table.Column<int>(type: "INTEGER", nullable: true),
                    ToPosition = table.Column<int>(type: "INTEGER", nullable: true),
                    Points = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Races",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Races", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Tag = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stages_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cyclists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cyclists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cyclists_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DraftTurns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TurnNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CyclistId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftTurns_Cyclists_CyclistId",
                        column: x => x.CyclistId,
                        principalTable: "Cyclists",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DraftTurns_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftTurns_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jersey",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StageId = table.Column<int>(type: "INTEGER", nullable: false),
                    CyclistId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jersey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jersey_Cyclists_CyclistId",
                        column: x => x.CyclistId,
                        principalTable: "Cyclists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Jersey_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    RaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    StageId = table.Column<int>(type: "INTEGER", nullable: true),
                    CyclistId = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPoints_Cyclists_CyclistId",
                        column: x => x.CyclistId,
                        principalTable: "Cyclists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerPoints_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerPoints_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerPoints_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    RaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    CyclistId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSelections_Cyclists_CyclistId",
                        column: x => x.CyclistId,
                        principalTable: "Cyclists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerSelections_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerSelections_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaceEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    CyclistId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaceEntries_Cyclists_CyclistId",
                        column: x => x.CyclistId,
                        principalTable: "Cyclists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaceEntries_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaceEntries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StageResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StageId = table.Column<int>(type: "INTEGER", nullable: false),
                    CyclistId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageResults_Cyclists_CyclistId",
                        column: x => x.CyclistId,
                        principalTable: "Cyclists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StageResults_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cyclists_TeamId",
                table: "Cyclists",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftTurns_CyclistId",
                table: "DraftTurns",
                column: "CyclistId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftTurns_PlayerId",
                table: "DraftTurns",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftTurns_RaceId_TurnNumber",
                table: "DraftTurns",
                columns: new[] { "RaceId", "TurnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jersey_CyclistId",
                table: "Jersey",
                column: "CyclistId");

            migrationBuilder.CreateIndex(
                name: "IX_Jersey_StageId",
                table: "Jersey",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoints_CyclistId",
                table: "PlayerPoints",
                column: "CyclistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoints_PlayerId",
                table: "PlayerPoints",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoints_RaceId",
                table: "PlayerPoints",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoints_StageId",
                table: "PlayerPoints",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSelections_CyclistId",
                table: "PlayerSelections",
                column: "CyclistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSelections_PlayerId_RaceId_CyclistId",
                table: "PlayerSelections",
                columns: new[] { "PlayerId", "RaceId", "CyclistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSelections_RaceId",
                table: "PlayerSelections",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceEntries_CyclistId",
                table: "RaceEntries",
                column: "CyclistId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceEntries_RaceId_CyclistId",
                table: "RaceEntries",
                columns: new[] { "RaceId", "CyclistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceEntries_TeamId",
                table: "RaceEntries",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Races_Name_Year",
                table: "Races",
                columns: new[] { "Name", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_CyclistId",
                table: "StageResults",
                column: "CyclistId");

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_StageId_CyclistId",
                table: "StageResults",
                columns: new[] { "StageId", "CyclistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stages_RaceId_StageNumber",
                table: "Stages",
                columns: new[] { "RaceId", "StageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Tag",
                table: "Teams",
                column: "Tag",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftTurns");

            migrationBuilder.DropTable(
                name: "Jersey");

            migrationBuilder.DropTable(
                name: "PlayerPoints");

            migrationBuilder.DropTable(
                name: "PlayerSelections");

            migrationBuilder.DropTable(
                name: "PointsRules");

            migrationBuilder.DropTable(
                name: "RaceEntries");

            migrationBuilder.DropTable(
                name: "StageResults");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Cyclists");

            migrationBuilder.DropTable(
                name: "Stages");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Races");
        }
    }
}
