using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddSePaySubscriptionModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "SePayWebhookLog");

            migrationBuilder.DropTable(
                name: "SubscriptionPayment");

            migrationBuilder.DropTable(
                name: "UserSubscription");
        }
    }
}
