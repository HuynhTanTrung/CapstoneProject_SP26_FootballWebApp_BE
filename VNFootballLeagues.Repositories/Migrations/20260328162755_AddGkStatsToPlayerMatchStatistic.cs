using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddGkStatsToPlayerMatchStatistic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoalsConceded",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HighClaims",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltiesSaved",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Punches",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunsOut",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunsOutSuccessful",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Saves",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SavesInsideBox",
                table: "PlayerMatchStatistics",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoalsConceded",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "HighClaims",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "PenaltiesSaved",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "Punches",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "RunsOut",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "RunsOutSuccessful",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "Saves",
                table: "PlayerMatchStatistics");

            migrationBuilder.DropColumn(
                name: "SavesInsideBox",
                table: "PlayerMatchStatistics");
        }
    }
}
