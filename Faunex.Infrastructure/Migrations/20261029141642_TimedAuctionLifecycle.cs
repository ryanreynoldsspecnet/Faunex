using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TimedAuctionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bids_AuctionId",
                table: "Bids");

            migrationBuilder.AddColumn<int>(
                name: "BidNumber",
                table: "Bids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BidCount",
                table: "Auctions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Auctions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Auctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LeadingAmount",
                table: "Auctions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadingBidId",
                table: "Auctions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadingBidderId",
                table: "Auctions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumIncrement",
                table: "Auctions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "Auctions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Revision",
                table: "Auctions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "RulesVersion",
                table: "Auctions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "Auctions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerId",
                table: "Auctions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WinningBidId",
                table: "Auctions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionId_BidNumber",
                table: "Bids",
                columns: new[] { "AuctionId", "BidNumber" },
                unique: true,
                filter: "\"BidNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_RulesVersion_State_EndsAt",
                table: "Auctions",
                columns: new[] { "RulesVersion", "State", "EndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bids_AuctionId_BidNumber",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_RulesVersion_State_EndsAt",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "BidNumber",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "BidCount",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "LeadingAmount",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "LeadingBidId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "LeadingBidderId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "MinimumIncrement",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "RulesVersion",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "WinnerId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "WinningBidId",
                table: "Auctions");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionId",
                table: "Bids",
                column: "AuctionId");
        }
    }
}
