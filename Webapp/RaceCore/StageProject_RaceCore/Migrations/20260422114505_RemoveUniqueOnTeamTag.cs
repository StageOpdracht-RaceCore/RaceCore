using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageProject_RaceCore.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueOnTeamTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Tag",
                table: "Teams");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Tag",
                table: "Teams",
                column: "Tag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Tag",
                table: "Teams");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Tag",
                table: "Teams",
                column: "Tag",
                unique: true);
        }
    }
}
