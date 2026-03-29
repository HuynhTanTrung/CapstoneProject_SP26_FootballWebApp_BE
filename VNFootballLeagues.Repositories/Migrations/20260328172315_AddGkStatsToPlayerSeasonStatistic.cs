using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddGkStatsToPlayerSeasonStatistic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CleanSheets",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalsConceded",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HighClaims",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltiesSaved",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Punches",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunsOut",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunsOutSuccessful",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Saves",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SavesInsideBox",
                table: "PlayerSeasonStatistics",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanSheets",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "GoalsConceded",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "HighClaims",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "PenaltiesSaved",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "Punches",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "RunsOut",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "RunsOutSuccessful",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "Saves",
                table: "PlayerSeasonStatistics");

            migrationBuilder.DropColumn(
                name: "SavesInsideBox",
                table: "PlayerSeasonStatistics");
        }
    }
}
