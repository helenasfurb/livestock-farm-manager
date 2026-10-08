using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_Vaccine_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Vaccines",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "Vaccines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_Vaccines_PropertyId_RowVersion",
                table: "Vaccines",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_Vaccines_SyncId",
                table: "Vaccines",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vaccines_PropertyId_RowVersion",
                table: "Vaccines");

            migrationBuilder.DropIndex(
                name: "UX_Vaccines_SyncId",
                table: "Vaccines");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Vaccines");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "Vaccines");
        }
    }
}
