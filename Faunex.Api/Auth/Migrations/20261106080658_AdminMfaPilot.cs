using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faunex.Api.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AdminMfaPilot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MfaFailureWindowEnd",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MfaFailures",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MfaLastStep",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MfaLoginNonce",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaProtectedKey",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaRecoveryHashes",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MfaFailureWindowEnd",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MfaFailures",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MfaLastStep",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MfaLoginNonce",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MfaProtectedKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MfaRecoveryHashes",
                table: "AspNetUsers");
        }
    }
}
