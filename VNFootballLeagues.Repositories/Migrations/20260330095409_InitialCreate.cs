using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Club",
                columns: table => new
                {
                    ClubId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FoundedYear = table.Column<int>(type: "int", nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Budget = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Club__D35058E7F99CA873", x => x.ClubId);
                });

            migrationBuilder.CreateTable(
                name: "League",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiLeagueId = table.Column<int>(type: "int", nullable: true),
                    LeagueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeagueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LogoUrl = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__League__10ABBCF494122950", x => x.LeagueId);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Role__8AFACE1A6BB4E1CE", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "SePayWebhookLog",
                columns: table => new
                {
                    WebhookLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    SePayTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TransferType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TransferAmount = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProcessingMessage = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SePayWebhookLog", x => x.WebhookLogId);
                });

            migrationBuilder.CreateTable(
                name: "Stadium",
                columns: table => new
                {
                    StadiumId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiVenueId = table.Column<int>(type: "int", nullable: true),
                    StadiumName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Surface = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ImageUrl = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsNationalStadium = table.Column<bool>(type: "bit", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Stadium__ED8330583874B553", x => x.StadiumId);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__User__1788CC4C6323F744", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Season",
                columns: table => new
                {
                    SeasonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: true),
                    ApiSeasonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Season__C1814E389AB36112", x => x.SeasonId);
                    table.ForeignKey(
                        name: "FK__Season__LeagueId__73BA3083",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "LeagueId");
                });

            migrationBuilder.CreateTable(
                name: "Team",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    ApiTeamId = table.Column<int>(type: "int", nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Founded = table.Column<int>(type: "int", nullable: true),
                    National = table.Column<bool>(type: "bit", nullable: false),
                    StadiumId = table.Column<int>(type: "int", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Team__123AE799A9C8BB32", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_Team_Club",
                        column: x => x.ClubId,
                        principalTable: "Club",
                        principalColumn: "ClubId");
                    table.ForeignKey(
                        name: "FK_Team_League",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "LeagueId");
                    table.ForeignKey(
                        name: "FK_Team_Stadium",
                        column: x => x.StadiumId,
                        principalTable: "Stadium",
                        principalColumn: "StadiumId");
                });

            migrationBuilder.CreateTable(
                name: "ChatSession",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ChatSess__C9F49290CFDB9472", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_ChatSession_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerificationToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__EmailVer__3214EC075394A7A3", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationToken_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Password__3214EC07EEFC636F", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetToken_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RefreshT__3214EC074AE78FFF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPayment",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    PaymentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TransferContent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QrUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SePayTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    SePayReferenceCode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Gateway = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SePayTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPayment", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_SubscriptionPayment_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRole_Role",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRole_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscription",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastPaymentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscription", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSubscription_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Match",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiFixtureId = table.Column<int>(type: "int", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    SeasonId = table.Column<int>(type: "int", nullable: true),
                    MatchDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    KickOffTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HomeTeamId = table.Column<int>(type: "int", nullable: true),
                    AwayTeamId = table.Column<int>(type: "int", nullable: true),
                    HomeGoals = table.Column<int>(type: "int", nullable: true),
                    AwayGoals = table.Column<int>(type: "int", nullable: true),
                    Venue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RefereeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Attendance = table.Column<int>(type: "int", nullable: true),
                    ApiTimestamp = table.Column<int>(type: "int", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PeriodFirstHalf = table.Column<int>(type: "int", nullable: true),
                    PeriodSecondHalf = table.Column<int>(type: "int", nullable: true),
                    Round = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApiVenueId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Match__4218C817300AB514", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK__Match__AwayTeamI__0E6E26BF",
                        column: x => x.AwayTeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK__Match__HomeTeamI__0D7A0286",
                        column: x => x.HomeTeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK__Match__LeagueId__0B91BA14",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "LeagueId");
                    table.ForeignKey(
                        name: "FK__Match__SeasonId__0C85DE4D",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "SeasonId");
                });

            migrationBuilder.CreateTable(
                name: "Player",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiPlayerId = table.Column<int>(type: "int", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BirthPlace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BirthCountry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    PhotoUrl = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    IsInjured = table.Column<bool>(type: "bit", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Number = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Player__4A4E74C8B01198BC", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK__Player__TeamId__02FC7413",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "Standings",
                columns: table => new
                {
                    StandingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    SeasonId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    Played = table.Column<int>(type: "int", nullable: true),
                    Win = table.Column<int>(type: "int", nullable: true),
                    Draw = table.Column<int>(type: "int", nullable: true),
                    Loss = table.Column<int>(type: "int", nullable: true),
                    GoalsFor = table.Column<int>(type: "int", nullable: true),
                    GoalsAgainst = table.Column<int>(type: "int", nullable: true),
                    GoalDifference = table.Column<int>(type: "int", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: true),
                    Form = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HomeRecord = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AwayRecord = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiLastUpdated = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Standing__FC2758C18EA6C935", x => x.StandingId);
                    table.ForeignKey(
                        name: "FK__Standings__Leagu__114A936A",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "LeagueId");
                    table.ForeignKey(
                        name: "FK__Standings__Seaso__123EB7A3",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "SeasonId");
                    table.ForeignKey(
                        name: "FK__Standings__TeamI__1332DBDC",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "TeamStatistics",
                columns: table => new
                {
                    TeamStatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    SeasonId = table.Column<int>(type: "int", nullable: true),
                    Played = table.Column<int>(type: "int", nullable: true),
                    Wins = table.Column<int>(type: "int", nullable: true),
                    Draws = table.Column<int>(type: "int", nullable: true),
                    Losses = table.Column<int>(type: "int", nullable: true),
                    GoalsFor = table.Column<int>(type: "int", nullable: true),
                    GoalsAgainst = table.Column<int>(type: "int", nullable: true),
                    CleanSheets = table.Column<int>(type: "int", nullable: true),
                    FailedToScore = table.Column<int>(type: "int", nullable: true),
                    Form = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HomePlayed = table.Column<int>(type: "int", nullable: true),
                    HomeWins = table.Column<int>(type: "int", nullable: true),
                    HomeDraws = table.Column<int>(type: "int", nullable: true),
                    HomeLosses = table.Column<int>(type: "int", nullable: true),
                    AwayPlayed = table.Column<int>(type: "int", nullable: true),
                    AwayWins = table.Column<int>(type: "int", nullable: true),
                    AwayDraws = table.Column<int>(type: "int", nullable: true),
                    AwayLosses = table.Column<int>(type: "int", nullable: true),
                    HomeGoalsFor = table.Column<int>(type: "int", nullable: true),
                    AwayGoalsFor = table.Column<int>(type: "int", nullable: true),
                    HomeGoalsAgainst = table.Column<int>(type: "int", nullable: true),
                    AwayGoalsAgainst = table.Column<int>(type: "int", nullable: true),
                    GoalsForAvgHome = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoalsForAvgAway = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoalsForAvgTotal = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoalsAgainstAvgHome = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoalsAgainstAvgAway = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoalsAgainstAvgTotal = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GoalsForMinute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoalsAgainstMinute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnderOverFor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnderOverAgainst = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BiggestStreakWins = table.Column<int>(type: "int", nullable: true),
                    BiggestStreakDraws = table.Column<int>(type: "int", nullable: true),
                    BiggestStreakLosses = table.Column<int>(type: "int", nullable: true),
                    BiggestWinHome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BiggestWinAway = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BiggestLossHome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BiggestLossAway = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PenaltiesScored = table.Column<int>(type: "int", nullable: true),
                    PenaltiesMissed = table.Column<int>(type: "int", nullable: true),
                    PenaltiesTotal = table.Column<int>(type: "int", nullable: true),
                    PenaltyPercentage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    YellowCardsMinute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedCardsMinute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CleanSheetsHome = table.Column<int>(type: "int", nullable: true),
                    CleanSheetsAway = table.Column<int>(type: "int", nullable: true),
                    FailedToScoreHome = table.Column<int>(type: "int", nullable: true),
                    FailedToScoreAway = table.Column<int>(type: "int", nullable: true),
                    BiggestGoalsForHome = table.Column<int>(type: "int", nullable: true),
                    BiggestGoalsForAway = table.Column<int>(type: "int", nullable: true),
                    BiggestGoalsAgainstHome = table.Column<int>(type: "int", nullable: true),
                    BiggestGoalsAgainstAway = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TeamStat__A0F24F8C8E4B5481", x => x.TeamStatId);
                    table.ForeignKey(
                        name: "FK__TeamStati__Leagu__32AB8735",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "LeagueId");
                    table.ForeignKey(
                        name: "FK__TeamStati__Seaso__339FAB6E",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "SeasonId");
                    table.ForeignKey(
                        name: "FK__TeamStati__TeamI__31B762FC",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "Message",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Message__C87C0C9CF30E6389", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Message_ChatSession",
                        column: x => x.SessionId,
                        principalTable: "ChatSession",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lineups",
                columns: table => new
                {
                    LineupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Formation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Lineups__492BA7DC1C443478", x => x.LineupId);
                    table.ForeignKey(
                        name: "FK__Lineups__MatchId__29221CFB",
                        column: x => x.MatchId,
                        principalTable: "Match",
                        principalColumn: "MatchId");
                    table.ForeignKey(
                        name: "FK__Lineups__TeamId__2A164134",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "MatchStatistics",
                columns: table => new
                {
                    StatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Possession = table.Column<int>(type: "int", nullable: true),
                    Shots = table.Column<int>(type: "int", nullable: true),
                    ShotsOnTarget = table.Column<int>(type: "int", nullable: true),
                    Corners = table.Column<int>(type: "int", nullable: true),
                    Fouls = table.Column<int>(type: "int", nullable: true),
                    YellowCards = table.Column<int>(type: "int", nullable: true),
                    RedCards = table.Column<int>(type: "int", nullable: true),
                    Offsides = table.Column<int>(type: "int", nullable: true),
                    ShotsBlocked = table.Column<int>(type: "int", nullable: true),
                    ShotsInsideBox = table.Column<int>(type: "int", nullable: true),
                    ShotsOutsideBox = table.Column<int>(type: "int", nullable: true),
                    PassesAccuracy = table.Column<int>(type: "int", nullable: true),
                    PassesKey = table.Column<int>(type: "int", nullable: true),
                    DribblesAttempted = table.Column<int>(type: "int", nullable: true),
                    DribblesSuccess = table.Column<int>(type: "int", nullable: true),
                    DuelsWon = table.Column<int>(type: "int", nullable: true),
                    DuelsTotal = table.Column<int>(type: "int", nullable: true),
                    TacklesWon = table.Column<int>(type: "int", nullable: true),
                    Saves = table.Column<int>(type: "int", nullable: true),
                    Interceptions = table.Column<int>(type: "int", nullable: true),
                    Clearances = table.Column<int>(type: "int", nullable: true),
                    ExpectedGoals = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__MatchSta__3A162D3E7BD005FF", x => x.StatId);
                    table.ForeignKey(
                        name: "FK__MatchStat__Match__208CD6FA",
                        column: x => x.MatchId,
                        principalTable: "Match",
                        principalColumn: "MatchId");
                    table.ForeignKey(
                        name: "FK__MatchStat__TeamI__2180FB33",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "Contract",
                columns: table => new
                {
                    ContractId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Contract__C90D3469F6B14076", x => x.ContractId);
                    table.ForeignKey(
                        name: "FK__Contract__Player__06CD04F7",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK__Contract__TeamId__07C12930",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiEventId = table.Column<int>(type: "int", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    AssistPlayerId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EventTime = table.Column<int>(type: "int", nullable: true),
                    ExtraTime = table.Column<int>(type: "int", nullable: true),
                    Period = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__MatchEve__7944C810C6F9983F", x => x.EventId);
                    table.ForeignKey(
                        name: "FK__MatchEven__Match__245D67DE",
                        column: x => x.MatchId,
                        principalTable: "Match",
                        principalColumn: "MatchId");
                    table.ForeignKey(
                        name: "FK__MatchEven__Playe__2645B050",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK__MatchEven__TeamI__25518C17",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "PlayerMatchStatistics",
                columns: table => new
                {
                    PlayerMatchStatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: true),
                    Goals = table.Column<int>(type: "int", nullable: true),
                    Assists = table.Column<int>(type: "int", nullable: true),
                    Shots = table.Column<int>(type: "int", nullable: true),
                    ShotsOnTarget = table.Column<int>(type: "int", nullable: true),
                    Passes = table.Column<int>(type: "int", nullable: true),
                    PassesAccuracy = table.Column<int>(type: "int", nullable: true),
                    PassesKey = table.Column<int>(type: "int", nullable: true),
                    TotalCrosses = table.Column<int>(type: "int", nullable: true),
                    AccurateCrosses = table.Column<int>(type: "int", nullable: true),
                    TotalLongBalls = table.Column<int>(type: "int", nullable: true),
                    AccurateLongBalls = table.Column<int>(type: "int", nullable: true),
                    PassesOwnHalf = table.Column<int>(type: "int", nullable: true),
                    AccuratePassesOwnHalf = table.Column<int>(type: "int", nullable: true),
                    PassesOppositionHalf = table.Column<int>(type: "int", nullable: true),
                    AccuratePassesOppositionHalf = table.Column<int>(type: "int", nullable: true),
                    Tackles = table.Column<int>(type: "int", nullable: true),
                    TacklesWon = table.Column<int>(type: "int", nullable: true),
                    Interceptions = table.Column<int>(type: "int", nullable: true),
                    Clearances = table.Column<int>(type: "int", nullable: true),
                    Blocks = table.Column<int>(type: "int", nullable: true),
                    DribblesAttempted = table.Column<int>(type: "int", nullable: true),
                    DribblesSuccess = table.Column<int>(type: "int", nullable: true),
                    DuelsWon = table.Column<int>(type: "int", nullable: true),
                    DuelsTotal = table.Column<int>(type: "int", nullable: true),
                    AerialDuelsWon = table.Column<int>(type: "int", nullable: true),
                    AerialDuelsLost = table.Column<int>(type: "int", nullable: true),
                    GroundDuelsWon = table.Column<int>(type: "int", nullable: true),
                    GroundDuelsLost = table.Column<int>(type: "int", nullable: true),
                    FoulsCommitted = table.Column<int>(type: "int", nullable: true),
                    FoulsDrawn = table.Column<int>(type: "int", nullable: true),
                    Offsides = table.Column<int>(type: "int", nullable: true),
                    YellowCards = table.Column<int>(type: "int", nullable: true),
                    RedCards = table.Column<int>(type: "int", nullable: true),
                    PenaltiesScored = table.Column<int>(type: "int", nullable: true),
                    PenaltiesMissed = table.Column<int>(type: "int", nullable: true),
                    PenaltiesWon = table.Column<int>(type: "int", nullable: true),
                    PenaltiesCommitted = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    ExpectedGoals = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ExpectedAssists = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Touches = table.Column<int>(type: "int", nullable: true),
                    PossessionLost = table.Column<int>(type: "int", nullable: true),
                    BallRecoveries = table.Column<int>(type: "int", nullable: true),
                    Dispossessed = table.Column<int>(type: "int", nullable: true),
                    WasFouled = table.Column<int>(type: "int", nullable: true),
                    UnsuccessfulTouch = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PlayerMa__7941EF4E6BB07C63", x => x.PlayerMatchStatId);
                    table.ForeignKey(
                        name: "FK__PlayerMat__Match__2CF2ADDF",
                        column: x => x.MatchId,
                        principalTable: "Match",
                        principalColumn: "MatchId");
                    table.ForeignKey(
                        name: "FK__PlayerMat__Playe__2DE6D218",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK__PlayerMat__TeamI__2EDAF651",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStatistics",
                columns: table => new
                {
                    PlayerStatisticsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    SeasonId = table.Column<int>(type: "int", nullable: true),
                    Appearances = table.Column<int>(type: "int", nullable: true),
                    Lineups = table.Column<int>(type: "int", nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: true),
                    Goals = table.Column<int>(type: "int", nullable: true),
                    Assists = table.Column<int>(type: "int", nullable: true),
                    YellowCards = table.Column<int>(type: "int", nullable: true),
                    RedCards = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    SubstitutionsIn = table.Column<int>(type: "int", nullable: true),
                    SubstitutionsOut = table.Column<int>(type: "int", nullable: true),
                    ShotsTotal = table.Column<int>(type: "int", nullable: true),
                    ShotsOnTarget = table.Column<int>(type: "int", nullable: true),
                    PassesTotal = table.Column<int>(type: "int", nullable: true),
                    PassesKey = table.Column<int>(type: "int", nullable: true),
                    PassesAccuracy = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DribblesAttempted = table.Column<int>(type: "int", nullable: true),
                    DribblesSuccess = table.Column<int>(type: "int", nullable: true),
                    DribblesSuccessRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DuelsWon = table.Column<int>(type: "int", nullable: true),
                    DuelsTotal = table.Column<int>(type: "int", nullable: true),
                    DuelsWonRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Tackles = table.Column<int>(type: "int", nullable: true),
                    Interceptions = table.Column<int>(type: "int", nullable: true),
                    FoulsDrawn = table.Column<int>(type: "int", nullable: true),
                    FoulsCommitted = table.Column<int>(type: "int", nullable: true),
                    PenaltiesScored = table.Column<int>(type: "int", nullable: true),
                    PenaltiesMissed = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PlayerSe__D4A80B8924468805", x => x.PlayerStatisticsId);
                    table.ForeignKey(
                        name: "FK__PlayerSea__Leagu__1CBC4616",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "LeagueId");
                    table.ForeignKey(
                        name: "FK__PlayerSea__Playe__1AD3FDA4",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK__PlayerSea__Seaso__1DB06A4F",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "SeasonId");
                    table.ForeignKey(
                        name: "FK__PlayerSea__TeamI__1BC821DD",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "Sidelined",
                columns: table => new
                {
                    SidelinedId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    SeasonId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SidelinedType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GamesMissed = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Sideline__0960E42DC0CFF9A6", x => x.SidelinedId);
                    table.ForeignKey(
                        name: "FK__Sidelined__Playe__3864608B",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK__Sidelined__Seaso__3A4CA8FD",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "SeasonId");
                    table.ForeignKey(
                        name: "FK__Sidelined__TeamI__395884C4",
                        column: x => x.TeamId,
                        principalTable: "Team",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    TransferId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    ApiTransferId = table.Column<int>(type: "int", nullable: false),
                    FromTeam = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ToTeam = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransferFee = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransferType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.TransferId);
                    table.ForeignKey(
                        name: "FK_Transfers_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Player",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_StartTime",
                table: "ChatSession",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSession_UserId",
                table: "ChatSession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_IsActive",
                table: "Contract",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_PlayerId",
                table: "Contract",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_TeamId",
                table: "Contract",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_Token",
                table: "EmailVerificationToken",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_UserId",
                table: "EmailVerificationToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ__League__A17F5C309759146E",
                table: "League",
                column: "ApiLeagueId",
                unique: true,
                filter: "[ApiLeagueId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Lineups_MatchId",
                table: "Lineups",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Lineups_TeamId",
                table: "Lineups",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Match_AwayTeamId",
                table: "Match",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Match_HomeTeamId",
                table: "Match",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Match_LeagueId",
                table: "Match",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Match_MatchDate",
                table: "Match",
                column: "MatchDate");

            migrationBuilder.CreateIndex(
                name: "IX_Match_SeasonId",
                table: "Match",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UQ__Match__B0C36CDA95B10723",
                table: "Match",
                column: "ApiFixtureId",
                unique: true,
                filter: "[ApiFixtureId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId",
                table: "MatchEvents",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_PlayerId",
                table: "MatchEvents",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_TeamId",
                table: "MatchEvents",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_MatchId",
                table: "MatchStatistics",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_TeamId",
                table: "MatchStatistics",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_SessionId",
                table: "Message",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_Timestamp",
                table: "Message",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetToken_Token",
                table: "PasswordResetToken",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetToken_UserId",
                table: "PasswordResetToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Player_Nationality",
                table: "Player",
                column: "Nationality");

            migrationBuilder.CreateIndex(
                name: "IX_Player_TeamId",
                table: "Player",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "UQ__Player__40FC95B48F6B9EFC",
                table: "Player",
                column: "ApiPlayerId",
                unique: true,
                filter: "[ApiPlayerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStatistics_MatchId",
                table: "PlayerMatchStatistics",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStatistics_PlayerId",
                table: "PlayerMatchStatistics",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStatistics_TeamId",
                table: "PlayerMatchStatistics",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatistics_LeagueId",
                table: "PlayerSeasonStatistics",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatistics_PlayerId",
                table: "PlayerSeasonStatistics",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatistics_SeasonId",
                table: "PlayerSeasonStatistics",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatistics_TeamId",
                table: "PlayerSeasonStatistics",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Role_RoleName",
                table: "Role",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Season_LeagueId",
                table: "Season",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_SePayWebhookLog_ProcessingStatus",
                table: "SePayWebhookLog",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SePayWebhookLog_ReceivedAt",
                table: "SePayWebhookLog",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SePayWebhookLog_SePayTransactionId",
                table: "SePayWebhookLog",
                column: "SePayTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sidelined_PlayerId",
                table: "Sidelined",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sidelined_SeasonId",
                table: "Sidelined",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Sidelined_TeamId",
                table: "Sidelined",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "UQ__Stadium__6F33BECC523957D7",
                table: "Stadium",
                column: "ApiVenueId",
                unique: true,
                filter: "[ApiVenueId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Standings_LeagueId_SeasonId",
                table: "Standings",
                columns: new[] { "LeagueId", "SeasonId" });

            migrationBuilder.CreateIndex(
                name: "IX_Standings_SeasonId",
                table: "Standings",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Standings_TeamId",
                table: "Standings",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayment_ExpiresAt",
                table: "SubscriptionPayment",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayment_PaymentCode",
                table: "SubscriptionPayment",
                column: "PaymentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayment_Status",
                table: "SubscriptionPayment",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayment_UserId",
                table: "SubscriptionPayment",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Team_ClubId",
                table: "Team",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Team_LeagueId",
                table: "Team",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Team_StadiumId",
                table: "Team",
                column: "StadiumId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamStatistics_LeagueId",
                table: "TeamStatistics",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamStatistics_SeasonId",
                table: "TeamStatistics",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamStatistics_TeamId",
                table: "TeamStatistics",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_PlayerId",
                table: "Transfers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Username",
                table: "User",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscription_ExpiresAt",
                table: "UserSubscription",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscription_Status",
                table: "UserSubscription",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contract");

            migrationBuilder.DropTable(
                name: "EmailVerificationToken");

            migrationBuilder.DropTable(
                name: "Lineups");

            migrationBuilder.DropTable(
                name: "MatchEvents");

            migrationBuilder.DropTable(
                name: "MatchStatistics");

            migrationBuilder.DropTable(
                name: "Message");

            migrationBuilder.DropTable(
                name: "PasswordResetToken");

            migrationBuilder.DropTable(
                name: "PlayerMatchStatistics");

            migrationBuilder.DropTable(
                name: "PlayerSeasonStatistics");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropTable(
                name: "SePayWebhookLog");

            migrationBuilder.DropTable(
                name: "Sidelined");

            migrationBuilder.DropTable(
                name: "Standings");

            migrationBuilder.DropTable(
                name: "SubscriptionPayment");

            migrationBuilder.DropTable(
                name: "TeamStatistics");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "UserSubscription");

            migrationBuilder.DropTable(
                name: "ChatSession");

            migrationBuilder.DropTable(
                name: "Match");

            migrationBuilder.DropTable(
                name: "Player");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Season");

            migrationBuilder.DropTable(
                name: "Team");

            migrationBuilder.DropTable(
                name: "Club");

            migrationBuilder.DropTable(
                name: "League");

            migrationBuilder.DropTable(
                name: "Stadium");
        }
    }
}
