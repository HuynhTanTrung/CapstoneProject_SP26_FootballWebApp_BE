using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddSofascoreRatingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerSeasonStatistics', 'SofascoreRating') IS NULL ALTER TABLE [dbo].[PlayerSeasonStatistics] ADD [SofascoreRating] decimal(18,2) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'SofascoreRating') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [SofascoreRating] decimal(18,2) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'IsExtraTime') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [IsExtraTime] bit NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'GoalsInExtraTime') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [GoalsInExtraTime] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'AssistsInExtraTime') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [AssistsInExtraTime] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutScored') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [PenaltyShootoutScored] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutMissed') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [PenaltyShootoutMissed] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutSaved') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [PenaltyShootoutSaved] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutConceded') IS NULL ALTER TABLE [dbo].[PlayerMatchStatistics] ADD [PenaltyShootoutConceded] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerSeasonStatistics', 'SofascoreRating') IS NOT NULL ALTER TABLE [dbo].[PlayerSeasonStatistics] DROP COLUMN [SofascoreRating];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'SofascoreRating') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [SofascoreRating];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'IsExtraTime') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [IsExtraTime];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'GoalsInExtraTime') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [GoalsInExtraTime];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'AssistsInExtraTime') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [AssistsInExtraTime];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutScored') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [PenaltyShootoutScored];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutMissed') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [PenaltyShootoutMissed];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutSaved') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [PenaltyShootoutSaved];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.PlayerMatchStatistics', 'PenaltyShootoutConceded') IS NOT NULL ALTER TABLE [dbo].[PlayerMatchStatistics] DROP COLUMN [PenaltyShootoutConceded];");
        }
    }
}
