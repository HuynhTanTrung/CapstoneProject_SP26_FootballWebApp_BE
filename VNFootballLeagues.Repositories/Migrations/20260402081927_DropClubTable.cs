using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class DropClubTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Team_Club",
                table: "Team");

            migrationBuilder.DropTable(
                name: "Club");

            migrationBuilder.DropIndex(
                name: "IX_Team_ClubId",
                table: "Team");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "Team");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubId",
                table: "Team",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Club",
                columns: table => new
                {
                    ClubId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Budget = table.Column<double>(type: "float", nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClubName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FoundedYear = table.Column<int>(type: "int", nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Club__D35058E7F99CA873", x => x.ClubId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Team_ClubId",
                table: "Team",
                column: "ClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_Team_Club",
                table: "Team",
                column: "ClubId",
                principalTable: "Club",
                principalColumn: "ClubId");
        }
    }
}
