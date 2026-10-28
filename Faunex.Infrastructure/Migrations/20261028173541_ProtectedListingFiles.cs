using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProtectedListingFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StoredFileId",
                table: "ListingDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ListingFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPhoto = table.Column<bool>(type: "boolean", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    OriginalSha256 = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingFiles_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingDocuments_StoredFileId",
                table: "ListingDocuments",
                column: "StoredFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingFiles_ListingId",
                table: "ListingFiles",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingFiles_StorageKey",
                table: "ListingFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingFiles_TenantId_ListingId",
                table: "ListingFiles",
                columns: new[] { "TenantId", "ListingId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ListingDocuments_ListingFiles_StoredFileId",
                table: "ListingDocuments",
                column: "StoredFileId",
                principalTable: "ListingFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListingDocuments_ListingFiles_StoredFileId",
                table: "ListingDocuments");

            migrationBuilder.DropTable(
                name: "ListingFiles");

            migrationBuilder.DropIndex(
                name: "IX_ListingDocuments_StoredFileId",
                table: "ListingDocuments");

            migrationBuilder.DropColumn(
                name: "StoredFileId",
                table: "ListingDocuments");
        }
    }
}
