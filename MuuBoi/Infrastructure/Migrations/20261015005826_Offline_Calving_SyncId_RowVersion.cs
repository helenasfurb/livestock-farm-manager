using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_Calving_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AnimalCalvings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "AnimalCalvings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AnimalCalvingCalves",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "AnimalCalvingCalves",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCalvings_PropertyId_RowVersion",
                table: "AnimalCalvings",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_AnimalCalvings_SyncId",
                table: "AnimalCalvings",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCalvingCalves_PropertyId_RowVersion",
                table: "AnimalCalvingCalves",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_AnimalCalvingCalves_SyncId",
                table: "AnimalCalvingCalves",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnimalCalvings_PropertyId_RowVersion",
                table: "AnimalCalvings");

            migrationBuilder.DropIndex(
                name: "UX_AnimalCalvings_SyncId",
                table: "AnimalCalvings");

            migrationBuilder.DropIndex(
                name: "IX_AnimalCalvingCalves_PropertyId_RowVersion",
                table: "AnimalCalvingCalves");

            migrationBuilder.DropIndex(
                name: "UX_AnimalCalvingCalves_SyncId",
                table: "AnimalCalvingCalves");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AnimalCalvings");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "AnimalCalvings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AnimalCalvingCalves");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "AnimalCalvingCalves");
        }
    }
}
