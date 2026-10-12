using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_Lactation_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Lactations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "Lactations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_PropertyId_RowVersion",
                table: "Lactations",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_Lactations_SyncId",
                table: "Lactations",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lactations_PropertyId_RowVersion",
                table: "Lactations");

            migrationBuilder.DropIndex(
                name: "UX_Lactations_SyncId",
                table: "Lactations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Lactations");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "Lactations");
        }
    }
}
