using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_SemenSampleMovement_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SemenSampleMovements",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "SemenSampleMovements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_SemenSampleMovements_PropertyId_RowVersion",
                table: "SemenSampleMovements",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_SemenSampleMovements_SyncId",
                table: "SemenSampleMovements",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SemenSampleMovements_PropertyId_RowVersion",
                table: "SemenSampleMovements");

            migrationBuilder.DropIndex(
                name: "UX_SemenSampleMovements_SyncId",
                table: "SemenSampleMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SemenSampleMovements");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "SemenSampleMovements");
        }
    }
}
