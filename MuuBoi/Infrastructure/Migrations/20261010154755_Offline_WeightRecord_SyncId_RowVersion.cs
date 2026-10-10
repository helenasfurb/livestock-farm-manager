using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_WeightRecord_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WeightRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "WeightRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_PropertyId_RowVersion",
                table: "WeightRecords",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_WeightRecords_SyncId",
                table: "WeightRecords",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeightRecords_PropertyId_RowVersion",
                table: "WeightRecords");

            migrationBuilder.DropIndex(
                name: "UX_WeightRecords_SyncId",
                table: "WeightRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WeightRecords");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "WeightRecords");
        }
    }
}
