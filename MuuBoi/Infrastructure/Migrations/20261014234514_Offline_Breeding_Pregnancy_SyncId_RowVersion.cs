using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_Breeding_Pregnancy_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "BreedingEvents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "BreedingEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AnimalPregnancies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "AnimalPregnancies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.Sql("UPDATE [AnimalPregnancies] SET [SyncId] = [ClientRequestId] WHERE [ClientRequestId] IS NOT NULL;");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_ClientRequestId",
                table: "AnimalPregnancies");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "AnimalPregnancies");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_PropertyId_RowVersion",
                table: "BreedingEvents",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_BreedingEvents_SyncId",
                table: "BreedingEvents",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_PropertyId_RowVersion",
                table: "AnimalPregnancies",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_AnimalPregnancies_SyncId",
                table: "AnimalPregnancies",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BreedingEvents_PropertyId_RowVersion",
                table: "BreedingEvents");

            migrationBuilder.DropIndex(
                name: "UX_BreedingEvents_SyncId",
                table: "BreedingEvents");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_PropertyId_RowVersion",
                table: "AnimalPregnancies");

            migrationBuilder.DropIndex(
                name: "UX_AnimalPregnancies_SyncId",
                table: "AnimalPregnancies");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BreedingEvents");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "BreedingEvents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AnimalPregnancies");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "AnimalPregnancies");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "AnimalPregnancies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_ClientRequestId",
                table: "AnimalPregnancies",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");
        }
    }
}
