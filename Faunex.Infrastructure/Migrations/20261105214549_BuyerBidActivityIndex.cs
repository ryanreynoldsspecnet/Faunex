using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BuyerBidActivityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Bids_TenantId_BidderId_PlacedAt_BidNumber_Id",
                table: "Bids",
                columns: new[] { "TenantId", "BidderId", "PlacedAt", "BidNumber", "Id" },
                descending: new[] { false, false, true, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bids_TenantId_BidderId_PlacedAt_BidNumber_Id",
                table: "Bids");
        }
    }
}
