using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuctionCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuctionCollectionId",
                table: "Listings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuctionCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionCollections", x => x.Id);
                    table.UniqueConstraint("AK_AuctionCollections_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AuctionCollections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_TenantId_AuctionCollectionId",
                table: "Listings",
                columns: new[] { "TenantId", "AuctionCollectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollections_TenantId_NormalizedName",
                table: "AuctionCollections",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_AuctionCollections_TenantId_AuctionCollectionId",
                table: "Listings",
                columns: new[] { "TenantId", "AuctionCollectionId" },
                principalTable: "AuctionCollections",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_AuctionCollections_TenantId_AuctionCollectionId",
                table: "Listings");

            migrationBuilder.DropTable(
                name: "AuctionCollections");

            migrationBuilder.DropIndex(
                name: "IX_Listings_TenantId_AuctionCollectionId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "AuctionCollectionId",
                table: "Listings");
        }
    }
}
