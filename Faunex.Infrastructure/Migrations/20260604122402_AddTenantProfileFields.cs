using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFirstName",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactLastName",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalAddress",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalAddress",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatNumber",
                table: "Tenants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ContactFirstName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ContactLastName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PhysicalAddress",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PostalAddress",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ShippingAddress",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "VatNumber",
                table: "Tenants");
        }
    }
}
