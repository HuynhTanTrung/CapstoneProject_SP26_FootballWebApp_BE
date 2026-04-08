using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    public partial class AddCosmeticsSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CosmeticItem' AND xtype='U')
CREATE TABLE [CosmeticItem] (
    [ItemId]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name]           NVARCHAR(100)  NOT NULL,
    [Description]    NVARCHAR(255)  NULL,
    [Category]       NVARCHAR(50)   NOT NULL,
    [UnlockType]     NVARCHAR(20)   NOT NULL DEFAULT 'shop',
    [PointCost]      INT            NULL,
    [AchievementKey] NVARCHAR(100)  NULL,
    [PreviewData]    NVARCHAR(500)  NULL,
    [IsActive]       BIT            NOT NULL DEFAULT 1,
    [CreatedAt]      DATETIME2      NOT NULL DEFAULT GETUTCDATE()
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserCosmetic' AND xtype='U')
CREATE TABLE [UserCosmetic] (
    [UserCosmeticId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId]         UNIQUEIDENTIFIER NOT NULL REFERENCES [User]([UserId]),
    [ItemId]         INT NOT NULL REFERENCES [CosmeticItem]([ItemId]) ON DELETE CASCADE,
    [AcquiredAt]     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IsEquipped]     BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_UserCosmetic UNIQUE ([UserId], [ItemId])
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserLoadout' AND xtype='U')
CREATE TABLE [UserLoadout] (
    [UserId]           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY REFERENCES [User]([UserId]),
    [FrameItemId]      INT NULL REFERENCES [CosmeticItem]([ItemId]),
    [NameColorItemId]  INT NULL REFERENCES [CosmeticItem]([ItemId]),
    [BannerItemId]     INT NULL REFERENCES [CosmeticItem]([ItemId]),
    [BadgeItemId]      INT NULL REFERENCES [CosmeticItem]([ItemId]),
    [EffectItemId]     INT NULL REFERENCES [CosmeticItem]([ItemId]),
    [CardItemId]       INT NULL REFERENCES [CosmeticItem]([ItemId]),
    [UpdatedAt]        DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
");

            // Seed data
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [CosmeticItem])
BEGIN
SET IDENTITY_INSERT [CosmeticItem] ON;
INSERT INTO [CosmeticItem] (ItemId,Name,Description,Category,UnlockType,PointCost,AchievementKey,PreviewData,IsActive,CreatedAt) VALUES
(1,N'Xanh dương',N'Màu tên xanh dương','nameColor','shop',5,NULL,'#3B82F6',1,GETUTCDATE()),
(2,N'Xanh lá',N'Màu tên xanh lá','nameColor','shop',8,NULL,'#22C55E',1,GETUTCDATE()),
(3,N'Tím',N'Màu tên tím','nameColor','shop',10,NULL,'#A855F7',1,GETUTCDATE()),
(4,N'Đỏ rực',N'Màu tên đỏ rực','nameColor','shop',15,NULL,'#EF4444',1,GETUTCDATE()),
(5,N'Cam',N'Màu tên cam','nameColor','shop',20,NULL,'#F97316',1,GETUTCDATE()),
(6,N'Hồng',N'Màu tên hồng','nameColor','shop',25,NULL,'#EC4899',1,GETUTCDATE()),
(7,N'Xanh neon',N'Màu tên xanh neon','nameColor','shop',50,NULL,'#00D9FF',1,GETUTCDATE()),
(8,N'Vàng gold',N'Màu tên vàng gold','nameColor','shop',150,NULL,'#F59E0B',1,GETUTCDATE()),
(9,N'Gradient rainbow',N'Màu tên gradient cầu vồng','nameColor','shop',300,NULL,'linear-gradient(90deg,#FF4444,#F97316,#F59E0B,#22C55E,#3B82F6,#A855F7)',1,GETUTCDATE()),
(10,N'Khung trắng',N'Khung avatar trắng đơn giản','frame','shop',20,NULL,'border:3px solid #ffffff',1,GETUTCDATE()),
(11,N'Khung xanh dương',N'Khung avatar xanh dương','frame','shop',30,NULL,'border:3px solid #3B82F6',1,GETUTCDATE()),
(12,N'Khung đỏ',N'Khung avatar đỏ','frame','shop',30,NULL,'border:3px solid #EF4444',1,GETUTCDATE()),
(13,N'Khung vàng',N'Khung avatar vàng','frame','shop',50,NULL,'border:3px solid #F59E0B',1,GETUTCDATE()),
(14,N'Khung holographic',N'Khung avatar holographic','frame','shop',150,NULL,'border:3px solid;border-image:linear-gradient(45deg,#FF4444,#F59E0B,#22C55E,#3B82F6) 1',1,GETUTCDATE()),
(15,N'Khung animated gradient',N'Khung avatar gradient động','frame','shop',200,NULL,'animated-gradient-border',1,GETUTCDATE()),
(16,N'Khung đồng',N'Điểm danh 7 ngày liên tiếp','frame','achievement',NULL,'streak_7','border:3px solid #CD7F32;box-shadow:0 0 8px #CD7F32',1,GETUTCDATE()),
(17,N'Khung lửa đỏ',N'Điểm danh 30 ngày liên tiếp','frame','achievement',NULL,'streak_30','border:3px solid #FF4444;box-shadow:0 0 12px #FF4444',1,GETUTCDATE()),
(18,N'Khung kim cương',N'Điểm danh 100 ngày liên tiếp','frame','achievement',NULL,'streak_100','border:3px solid #00D9FF;box-shadow:0 0 16px #00D9FF',1,GETUTCDATE()),
(19,N'Tiên tri',N'Dự đoán đúng 10 trận','badge','achievement',NULL,'correct_10','⚽',1,GETUTCDATE()),
(20,N'Bắn tỉa',N'Dự đoán đúng tỉ số 10 lần','badge','achievement',NULL,'exact_10','🎯',1,GETUTCDATE()),
(21,N'Huyền thoại',N'Dự đoán đúng 50 trận','badge','achievement',NULL,'correct_50','👑',1,GETUTCDATE()),
(22,N'Rising Star',N'Badge ngôi sao đang lên','badge','shop',10,NULL,'🌟',1,GETUTCDATE()),
(23,N'Lightning',N'Badge sấm sét','badge','shop',20,NULL,'⚡',1,GETUTCDATE()),
(24,N'Lion',N'Badge sư tử','badge','shop',30,NULL,'🦁',1,GETUTCDATE()),
(25,N'Champion',N'Badge nhà vô địch','badge','shop',50,NULL,'🏆',1,GETUTCDATE()),
(26,N'King',N'Badge vua','badge','shop',80,NULL,'👑',1,GETUTCDATE()),
(27,N'Legend',N'Badge huyền thoại','badge','shop',100,NULL,'💎',1,GETUTCDATE()),
(28,N'Banner sân cỏ',N'Banner nền sân cỏ xanh','banner','shop',30,NULL,'bg-green-field',1,GETUTCDATE()),
(29,N'Banner đêm sân vận động',N'Banner đêm sân vận động','banner','shop',50,NULL,'bg-stadium-night',1,GETUTCDATE()),
(30,N'Banner confetti',N'Banner confetti chiến thắng','banner','shop',80,NULL,'bg-confetti',1,GETUTCDATE()),
(31,N'Banner animated',N'Banner mưa sao pháo hoa','banner','shop',200,NULL,'bg-animated-stars',1,GETUTCDATE()),
(32,N'Banner holographic',N'Banner holographic','banner','shop',250,NULL,'bg-holographic',1,GETUTCDATE()),
(33,N'Confetti profile',N'Hiệu ứng confetti khi vào profile','effect','shop',50,NULL,'effect-confetti',1,GETUTCDATE()),
(34,N'Bóng nảy',N'Hiệu ứng bóng nảy xung quanh avatar','effect','shop',80,NULL,'effect-bouncing-ball',1,GETUTCDATE()),
(35,N'Lửa cháy',N'Hiệu ứng lửa cháy quanh avatar','effect','shop',150,NULL,'effect-fire',1,GETUTCDATE()),
(36,N'Sấm sét',N'Hiệu ứng sấm sét','effect','shop',200,NULL,'effect-lightning',1,GETUTCDATE()),
(37,N'Kim cương rơi',N'Hiệu ứng kim cương rơi','effect','shop',300,NULL,'effect-diamonds',1,GETUTCDATE()),
(38,N'Card bạc',N'Card profile bạc','card','shop',100,NULL,'card-silver',1,GETUTCDATE()),
(39,N'Card vàng',N'Card profile vàng','card','shop',200,NULL,'card-gold',1,GETUTCDATE()),
(40,N'Card holographic',N'Card profile holographic','card','shop',350,NULL,'card-holographic',1,GETUTCDATE()),
(41,N'Card animated',N'Card profile animated','card','shop',500,NULL,'card-animated',1,GETUTCDATE());
SET IDENTITY_INSERT [CosmeticItem] OFF;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS [UserLoadout];
DROP TABLE IF EXISTS [UserCosmetic];
DROP TABLE IF EXISTS [CosmeticItem];
");
        }
    }
}
