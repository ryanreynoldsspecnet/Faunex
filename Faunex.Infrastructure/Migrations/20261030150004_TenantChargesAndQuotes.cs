using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantChargesAndQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargeTermsJson",
                table: "Auctions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChargeTermsJson",
                table: "AuctionOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderPriceQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PricingJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPriceQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPriceQuotes_AuctionOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "AuctionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderPriceQuotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantChargeCatalogs",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    WriteStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Terms = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantChargeCatalogs", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantChargeCatalogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPriceQuotes_OrderId",
                table: "OrderPriceQuotes",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPriceQuotes_TenantId",
                table: "OrderPriceQuotes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderPriceQuotes");

            migrationBuilder.DropTable(
                name: "TenantChargeCatalogs");

            migrationBuilder.DropColumn(
                name: "ChargeTermsJson",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ChargeTermsJson",
                table: "AuctionOrders");
        }
    }
}
