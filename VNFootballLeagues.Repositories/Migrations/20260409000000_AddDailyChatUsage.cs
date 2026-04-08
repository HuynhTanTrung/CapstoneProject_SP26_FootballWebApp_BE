using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    public partial class AddDailyChatUsage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DailyChatUsage' AND xtype='U')
CREATE TABLE [DailyChatUsage] (
    [Id]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId]    UNIQUEIDENTIFIER NOT NULL REFERENCES [User]([UserId]) ON DELETE CASCADE,
    [UsageDate] DATE NOT NULL,
    [Count]     INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_DailyChatUsage UNIQUE ([UserId], [UsageDate])
);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [DailyChatUsage];");
        }
    }
}
