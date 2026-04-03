using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VideoFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAnalyses_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalyses_UserId",
                table: "VideoAnalyses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAnalyses");
        }
    }
}
