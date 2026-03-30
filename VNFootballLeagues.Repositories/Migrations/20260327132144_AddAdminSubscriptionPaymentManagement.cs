using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNFootballLeagues.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSubscriptionPaymentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualUpdateReason",
                table: "SubscriptionPayment",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManualUpdatedAt",
                table: "SubscriptionPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualUpdatedByName",
                table: "SubscriptionPayment",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManualUpdatedByUserId",
                table: "SubscriptionPayment",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualUpdateReason",
                table: "SubscriptionPayment");

            migrationBuilder.DropColumn(
                name: "ManualUpdatedAt",
                table: "SubscriptionPayment");

            migrationBuilder.DropColumn(
                name: "ManualUpdatedByName",
                table: "SubscriptionPayment");

            migrationBuilder.DropColumn(
                name: "ManualUpdatedByUserId",
                table: "SubscriptionPayment");
        }
    }
}
