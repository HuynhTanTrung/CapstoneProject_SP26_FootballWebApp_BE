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
            // Drop FK if exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Team_Club')
                    ALTER TABLE [Team] DROP CONSTRAINT [FK_Team_Club];
            ");

            // Drop Club table if exists
            migrationBuilder.Sql(@"
                IF OBJECT_ID('Club', 'U') IS NOT NULL DROP TABLE [Club];
            ");

            // Use IF EXISTS to avoid error if index was already dropped
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Team_ClubId' AND object_id = OBJECT_ID('Team'))
                    DROP INDEX [IX_Team_ClubId] ON [Team];
            ");

            // Drop ClubId column if it exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ClubId' AND object_id = OBJECT_ID('Team'))
                    ALTER TABLE [Team] DROP COLUMN [ClubId];
            ");
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
