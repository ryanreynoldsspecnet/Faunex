using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BirdListingEditor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Revision",
                table: "Listings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateOnly>(
                name: "HatchDate",
                table: "BirdDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MicrochipNumber",
                table: "BirdDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RingNumber",
                table: "BirdDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "BirdDetails",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "HatchDate",
                table: "BirdDetails");

            migrationBuilder.DropColumn(
                name: "MicrochipNumber",
                table: "BirdDetails");

            migrationBuilder.DropColumn(
                name: "RingNumber",
                table: "BirdDetails");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "BirdDetails");
        }
    }
}
