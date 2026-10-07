using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_SemenSample_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchDate",
                table: "SemenSamples");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SemenSamples",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "SemenSamples",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_SemenSamples_PropertyId_RowVersion",
                table: "SemenSamples",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_SemenSamples_SyncId",
                table: "SemenSamples",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SemenSamples_PropertyId_RowVersion",
                table: "SemenSamples");

            migrationBuilder.DropIndex(
                name: "UX_SemenSamples_SyncId",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "SemenSamples");

            migrationBuilder.AddColumn<DateTime>(
                name: "BatchDate",
                table: "SemenSamples",
                type: "datetime2",
                nullable: true);
        }
    }
}
