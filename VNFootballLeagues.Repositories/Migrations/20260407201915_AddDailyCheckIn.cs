using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    public partial class AddDailyCheckIn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add missing columns to PlayerMatchStatistics (safe - only if not exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'AssistsInExtraTime')
                    ALTER TABLE [PlayerMatchStatistics] ADD [AssistsInExtraTime] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'GoalsInExtraTime')
                    ALTER TABLE [PlayerMatchStatistics] ADD [GoalsInExtraTime] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'IsExtraTime')
                    ALTER TABLE [PlayerMatchStatistics] ADD [IsExtraTime] bit NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'PenaltyShootoutConceded')
                    ALTER TABLE [PlayerMatchStatistics] ADD [PenaltyShootoutConceded] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'PenaltyShootoutMissed')
                    ALTER TABLE [PlayerMatchStatistics] ADD [PenaltyShootoutMissed] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'PenaltyShootoutSaved')
                    ALTER TABLE [PlayerMatchStatistics] ADD [PenaltyShootoutSaved] int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerMatchStatistics') AND name = 'PenaltyShootoutScored')
                    ALTER TABLE [PlayerMatchStatistics] ADD [PenaltyShootoutScored] int NULL;
            ");

            // Create DailyCheckIn table
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DailyCheckIn' AND xtype='U')
                CREATE TABLE [DailyCheckIn] (
                    [CheckInId]    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId]       UNIQUEIDENTIFIER  NOT NULL REFERENCES [User]([UserId]) ON DELETE CASCADE,
                    [CheckInDate]  DATE              NOT NULL,
                    [Streak]       INT               NOT NULL DEFAULT 1,
                    [PointsEarned] INT               NOT NULL DEFAULT 1,
                    CONSTRAINT UQ_DailyCheckIn_User_Date UNIQUE ([UserId], [CheckInDate])
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [DailyCheckIn];");
        }
    }
}
