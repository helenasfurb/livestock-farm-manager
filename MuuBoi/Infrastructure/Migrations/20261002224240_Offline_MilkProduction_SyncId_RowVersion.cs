using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_MilkProduction_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MilkProductions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "MilkProductions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_MilkProductions_PropertyId_RowVersion",
                table: "MilkProductions",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_MilkProductions_SyncId",
                table: "MilkProductions",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MilkProductions_PropertyId_RowVersion",
                table: "MilkProductions");

            migrationBuilder.DropIndex(
                name: "UX_MilkProductions_SyncId",
                table: "MilkProductions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MilkProductions");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "MilkProductions");
        }
    }
}
