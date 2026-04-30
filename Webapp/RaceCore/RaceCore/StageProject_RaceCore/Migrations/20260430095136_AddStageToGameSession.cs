using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    public partial class AddStageToGameSession : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Oude index alleen verwijderen als die bestaat
            migrationBuilder.Sql(@"
                SET @sql = IF(
                    EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'DraftTurns'
                        AND INDEX_NAME = 'IX_DraftTurns_RaceId_TurnNumber'
                    ),
                    'ALTER TABLE DraftTurns DROP INDEX IX_DraftTurns_RaceId_TurnNumber',
                    'SELECT 1'
                );

                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // StageId toevoegen als die nog niet bestaat
            migrationBuilder.Sql(@"
                ALTER TABLE GameSessions
                ADD COLUMN IF NOT EXISTS StageId INT NULL;
            ");

            // Bestaande games automatisch koppelen aan eerste rit van hun race
            migrationBuilder.Sql(@"
                UPDATE GameSessions gs
                SET gs.StageId = (
                    SELECT s.Id
                    FROM Stages s
                    WHERE s.RaceId = gs.RaceId
                    ORDER BY s.StageNumber
                    LIMIT 1
                )
                WHERE gs.StageId IS NULL;
            ");

            // StageId verplicht maken
            migrationBuilder.Sql(@"
                ALTER TABLE GameSessions
                MODIFY COLUMN StageId INT NOT NULL;
            ");

            // Nieuwe correcte index maken voor DraftTurns
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_DraftTurns_GameSessionId_TurnNumber
                ON DraftTurns (GameSessionId, TurnNumber);
            ");

            // Index voor StageId maken
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_GameSessions_StageId
                ON GameSessions (StageId);
            ");

            // FK Race terug toevoegen als die niet bestaat
            migrationBuilder.Sql(@"
                SET @sql = IF(
                    NOT EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                        WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'GameSessions'
                        AND CONSTRAINT_NAME = 'FK_GameSessions_Races_RaceId'
                    ),
                    'ALTER TABLE GameSessions ADD CONSTRAINT FK_GameSessions_Races_RaceId FOREIGN KEY (RaceId) REFERENCES Races(Id) ON DELETE RESTRICT',
                    'SELECT 1'
                );

                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // FK Stage toevoegen als die nog niet bestaat
            migrationBuilder.Sql(@"
                SET @sql = IF(
                    NOT EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                        WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'GameSessions'
                        AND CONSTRAINT_NAME = 'FK_GameSessions_Stages_StageId'
                    ),
                    'ALTER TABLE GameSessions ADD CONSTRAINT FK_GameSessions_Stages_StageId FOREIGN KEY (StageId) REFERENCES Stages(Id) ON DELETE RESTRICT',
                    'SELECT 1'
                );

                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE GameSessions
                DROP FOREIGN KEY IF EXISTS FK_GameSessions_Stages_StageId;
            ");

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS IX_GameSessions_StageId ON GameSessions;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE GameSessions
                DROP COLUMN IF EXISTS StageId;
            ");
        }
    }
}