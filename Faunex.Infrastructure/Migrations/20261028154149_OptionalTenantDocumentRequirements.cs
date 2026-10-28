using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptionalTenantDocumentRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingDocumentRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalClass = table.Column<string>(type: "text", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingDocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingDocumentRequirements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingDocumentRequirements_TenantId_AnimalClass_DocumentTy~",
                table: "ListingDocumentRequirements",
                columns: new[] { "TenantId", "AnimalClass", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingDocumentRequirements");
        }
    }
}
