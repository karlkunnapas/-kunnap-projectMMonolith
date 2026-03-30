using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySlugAndMembershipConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUserCompanies_AppUserId",
                table: "AppUserCompanies");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Companies",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AppUserCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedAtUtc",
                table: "AppUserCompanies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "AppUserCompanies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserCompanies_AppUserId_CompanyId",
                table: "AppUserCompanies",
                columns: new[] { "AppUserId", "CompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_Slug",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_AppUserCompanies_AppUserId_CompanyId",
                table: "AppUserCompanies");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AppUserCompanies");

            migrationBuilder.DropColumn(
                name: "JoinedAtUtc",
                table: "AppUserCompanies");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppUserCompanies");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserCompanies_AppUserId",
                table: "AppUserCompanies",
                column: "AppUserId");
        }
    }
}
