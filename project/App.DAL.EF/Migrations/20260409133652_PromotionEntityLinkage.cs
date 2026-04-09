using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class PromotionEntityLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                table: "ChargingSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PromotionId",
                table: "Reservations",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSessions_PromotionId",
                table: "ChargingSessions",
                column: "PromotionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingSessions_Promotions_PromotionId",
                table: "ChargingSessions",
                column: "PromotionId",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Promotions_PromotionId",
                table: "Reservations",
                column: "PromotionId",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingSessions_Promotions_PromotionId",
                table: "ChargingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Promotions_PromotionId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_PromotionId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_ChargingSessions_PromotionId",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                table: "ChargingSessions");
        }
    }
}
