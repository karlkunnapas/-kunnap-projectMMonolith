using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class LangStrJsonbFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'ChargingStations' AND column_name = 'Name'
                    ) THEN
                        ALTER TABLE "ChargingStations" ADD COLUMN "Name" jsonb NOT NULL DEFAULT '{}'::jsonb;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Connectors' AND column_name = 'Name'
                    ) THEN
                        ALTER TABLE "Connectors" ADD COLUMN "Name" jsonb NOT NULL DEFAULT '{}'::jsonb;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Companies' AND column_name = 'Name'
                    ) THEN
                        ALTER TABLE "Companies" ADD COLUMN "Name" jsonb NOT NULL DEFAULT '{}'::jsonb;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'LangStr' AND column_name = 'TranslationsJson'
                    ) THEN
                        UPDATE "ChargingStations" cs
                        SET "Name" = COALESCE(ls."TranslationsJson"::jsonb, '{}'::jsonb)
                        FROM "LangStr" ls
                        WHERE cs."NameId" = ls."Id";

                        UPDATE "Connectors" c
                        SET "Name" = COALESCE(ls."TranslationsJson"::jsonb, '{}'::jsonb)
                        FROM "LangStr" ls
                        WHERE c."NameId" = ls."Id";

                        UPDATE "Companies" c
                        SET "Name" = COALESCE(ls."TranslationsJson"::jsonb, '{}'::jsonb)
                        FROM "LangStr" ls
                        WHERE c."NameId" = ls."Id";
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ChargingStations_LangStr_NameId') THEN
                        ALTER TABLE "ChargingStations" DROP CONSTRAINT "FK_ChargingStations_LangStr_NameId";
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Companies_LangStr_NameId') THEN
                        ALTER TABLE "Companies" DROP CONSTRAINT "FK_Companies_LangStr_NameId";
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Connectors_LangStr_NameId') THEN
                        ALTER TABLE "Connectors" DROP CONSTRAINT "FK_Connectors_LangStr_NameId";
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'IX_Connectors_NameId') THEN
                        DROP INDEX "IX_Connectors_NameId";
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'IX_Companies_NameId') THEN
                        DROP INDEX "IX_Companies_NameId";
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'IX_ChargingStations_NameId') THEN
                        DROP INDEX "IX_ChargingStations_NameId";
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Connectors' AND column_name = 'NameId'
                    ) THEN
                        ALTER TABLE "Connectors" DROP COLUMN "NameId";
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Companies' AND column_name = 'NameId'
                    ) THEN
                        ALTER TABLE "Companies" DROP COLUMN "NameId";
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'ChargingStations' AND column_name = 'NameId'
                    ) THEN
                        ALTER TABLE "ChargingStations" DROP COLUMN "NameId";
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_name = 'LangStr'
                    ) THEN
                        DROP TABLE "LangStr";
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NameId",
                table: "Connectors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "NameId",
                table: "Companies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "NameId",
                table: "ChargingStations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "LangStr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LangStr", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_NameId",
                table: "Connectors",
                column: "NameId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NameId",
                table: "Companies",
                column: "NameId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingStations_NameId",
                table: "ChargingStations",
                column: "NameId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingStations_LangStr_NameId",
                table: "ChargingStations",
                column: "NameId",
                principalTable: "LangStr",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_LangStr_NameId",
                table: "Companies",
                column: "NameId",
                principalTable: "LangStr",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Connectors_LangStr_NameId",
                table: "Connectors",
                column: "NameId",
                principalTable: "LangStr",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
