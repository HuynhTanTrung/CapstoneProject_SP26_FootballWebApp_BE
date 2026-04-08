using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    // Tables/columns already created via direct SQL. This migration only updates the EF snapshot.
    public partial class AddForumReactionsAndViewCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ViewCount if not exists
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ForumPost') AND name='ViewCount')
                    ALTER TABLE ForumPost ADD ViewCount INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ForumComment') AND name='ParentCommentId')
                    ALTER TABLE ForumComment ADD ParentCommentId INT NULL REFERENCES ForumComment(CommentId);
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ForumReaction' AND xtype='U')
                CREATE TABLE [ForumReaction] (
                    [ReactionId]   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [PostId]       INT NOT NULL REFERENCES ForumPost(PostId) ON DELETE CASCADE,
                    [UserId]       UNIQUEIDENTIFIER NOT NULL REFERENCES [User](UserId),
                    [ReactionType] NVARCHAR(20) NOT NULL,
                    [CreatedAt]    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT UQ_ForumReaction UNIQUE ([PostId], [UserId])
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
