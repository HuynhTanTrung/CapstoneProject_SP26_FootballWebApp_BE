using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    public partial class AddPredictionContests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PredictionContests' AND xtype='U')
CREATE TABLE [PredictionContests] (
    [ContestId]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [ContestType]   NVARCHAR(20)  NOT NULL,
    [Title]         NVARCHAR(200) NOT NULL,
    [Description]   NVARCHAR(500) NULL,
    [ClosesAt]      DATETIME2     NOT NULL,
    [ResultAt]      DATETIME2     NULL,
    [PointsExact]   INT           NOT NULL DEFAULT 0,
    [PointsPartial] INT           NOT NULL DEFAULT 0,
    [Status]        NVARCHAR(20)  NOT NULL DEFAULT 'OPEN',
    [LeagueId]      INT           NULL REFERENCES [League]([LeagueId]),
    [SeasonId]      INT           NULL REFERENCES [Season]([SeasonId]),
    [CreatedAt]     DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ContestEntries' AND xtype='U')
CREATE TABLE [ContestEntries] (
    [EntryId]    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [ContestId]  INT           NOT NULL REFERENCES [PredictionContests]([ContestId]),
    [UserId]     UNIQUEIDENTIFIER NOT NULL REFERENCES [User]([UserId]),
    [Rank]       INT           NOT NULL DEFAULT 1,
    [TeamId]     INT           NULL REFERENCES [Team]([TeamId]),
    [PlayerId]   INT           NULL REFERENCES [Player]([PlayerId]),
    [CreatedAt]  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    [Points]     INT           NULL,
    [IsCorrect]  INT           NULL
);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ContestResults' AND xtype='U')
CREATE TABLE [ContestResults] (
    [ResultId]   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [ContestId]  INT NOT NULL REFERENCES [PredictionContests]([ContestId]),
    [Rank]       INT NOT NULL DEFAULT 1,
    [TeamId]     INT NULL REFERENCES [Team]([TeamId]),
    [PlayerId]   INT NULL REFERENCES [Player]([PlayerId]),
    [CreatedAt]  DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sysobjects WHERE name='ContestResults' AND xtype='U') DROP TABLE [ContestResults];");
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sysobjects WHERE name='ContestEntries' AND xtype='U') DROP TABLE [ContestEntries];");
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sysobjects WHERE name='PredictionContests' AND xtype='U') DROP TABLE [PredictionContests];");
        }
    }
}
