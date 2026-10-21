using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_Stock_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockMovements",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "StockItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PropertyId_RowVersion",
                table: "StockMovements",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_SyncId",
                table: "StockMovements",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_PropertyId_RowVersion",
                table: "StockItems",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_StockItems_SyncId",
                table: "StockItems",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PropertyId_RowVersion",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "UX_StockMovements_SyncId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockItems_PropertyId_RowVersion",
                table: "StockItems");

            migrationBuilder.DropIndex(
                name: "UX_StockItems_SyncId",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "StockItems");
        }
    }
}
