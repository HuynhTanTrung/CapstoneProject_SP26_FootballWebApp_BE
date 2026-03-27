using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddContractsAndTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "Lineups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayersJson",
                table: "Lineups",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
