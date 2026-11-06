using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixedPriceOffersAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "WinningBidId",
                table: "AuctionOrders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuctionId",
                table: "AuctionOrders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "CheckoutStarted",
                table: "AuctionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedPriceOfferId",
                table: "AuctionOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                table: "AuctionOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FixedPriceOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CounterAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedPriceOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedPriceOffers_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_FixedPriceOfferId",
                table: "AuctionOrders",
                column: "FixedPriceOfferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_TenantId_ListingId",
                table: "AuctionOrders",
                columns: new[] { "TenantId", "ListingId" },
                unique: true,
                filter: "\"AuctionId\" IS NULL AND \"ReleasedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuctionOrders_Source",
                table: "AuctionOrders",
                sql: "(\"AuctionId\" IS NULL AND \"WinningBidId\" IS NULL AND (\"ReleasedAt\" IS NULL OR NOT \"CheckoutStarted\")) OR (\"AuctionId\" IS NOT NULL AND \"WinningBidId\" IS NOT NULL AND \"FixedPriceOfferId\" IS NULL AND \"ReleasedAt\" IS NULL AND NOT \"CheckoutStarted\")");

            migrationBuilder.CreateIndex(
                name: "IX_FixedPriceOffers_ListingId",
                table: "FixedPriceOffers",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedPriceOffers_TenantId_BuyerId_Id",
                table: "FixedPriceOffers",
                columns: new[] { "TenantId", "BuyerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedPriceOffers_TenantId_ListingId_BuyerId",
                table: "FixedPriceOffers",
                columns: new[] { "TenantId", "ListingId", "BuyerId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Countered')");

            migrationBuilder.CreateIndex(
                name: "IX_FixedPriceOffers_TenantId_SellerId_Id",
                table: "FixedPriceOffers",
                columns: new[] { "TenantId", "SellerId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuctionOrders_FixedPriceOffers_FixedPriceOfferId",
                table: "AuctionOrders",
                column: "FixedPriceOfferId",
                principalTable: "FixedPriceOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuctionOrders_FixedPriceOffers_FixedPriceOfferId",
                table: "AuctionOrders");

            migrationBuilder.DropTable(
                name: "FixedPriceOffers");

            migrationBuilder.DropIndex(
                name: "IX_AuctionOrders_FixedPriceOfferId",
                table: "AuctionOrders");

            migrationBuilder.DropIndex(
                name: "IX_AuctionOrders_TenantId_ListingId",
                table: "AuctionOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuctionOrders_Source",
                table: "AuctionOrders");

            migrationBuilder.DropColumn(
                name: "CheckoutStarted",
                table: "AuctionOrders");

            migrationBuilder.DropColumn(
                name: "FixedPriceOfferId",
                table: "AuctionOrders");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "AuctionOrders");

            migrationBuilder.AlterColumn<Guid>(
                name: "WinningBidId",
                table: "AuctionOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AuctionId",
                table: "AuctionOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
