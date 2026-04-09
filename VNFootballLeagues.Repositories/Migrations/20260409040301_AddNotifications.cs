using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    // Notification table already exists. This migration only updates the EF snapshot.
    public partial class AddNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notification' AND xtype='U')
                CREATE TABLE [Notification] (
                    [NotificationId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId]         UNIQUEIDENTIFIER NOT NULL REFERENCES [User]([UserId]) ON DELETE CASCADE,
                    [Type]           NVARCHAR(50)  NOT NULL,
                    [Title]          NVARCHAR(200) NOT NULL,
                    [Message]        NVARCHAR(1000) NOT NULL,
                    [Link]           NVARCHAR(500) NULL,
                    [IsRead]         BIT NOT NULL DEFAULT 0,
                    [CreatedAt]      DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Notification_UserId_IsRead')
                    CREATE INDEX IX_Notification_UserId_IsRead ON [Notification]([UserId], [IsRead]);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [Notification];");
        }
    }
}
