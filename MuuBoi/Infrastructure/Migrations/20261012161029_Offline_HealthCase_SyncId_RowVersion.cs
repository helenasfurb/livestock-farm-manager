using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_HealthCase_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MastitisTests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "MastitisTests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "HealthCases",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "HealthCases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AnimalMedications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "AnimalMedications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_MastitisTests_PropertyId_RowVersion",
                table: "MastitisTests",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_MastitisTests_SyncId",
                table: "MastitisTests",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthCases_PropertyId_RowVersion",
                table: "HealthCases",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_HealthCases_SyncId",
                table: "HealthCases",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalMedications_PropertyId_RowVersion",
                table: "AnimalMedications",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_AnimalMedications_SyncId",
                table: "AnimalMedications",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MastitisTests_PropertyId_RowVersion",
                table: "MastitisTests");

            migrationBuilder.DropIndex(
                name: "UX_MastitisTests_SyncId",
                table: "MastitisTests");

            migrationBuilder.DropIndex(
                name: "IX_HealthCases_PropertyId_RowVersion",
                table: "HealthCases");

            migrationBuilder.DropIndex(
                name: "UX_HealthCases_SyncId",
                table: "HealthCases");

            migrationBuilder.DropIndex(
                name: "IX_AnimalMedications_PropertyId_RowVersion",
                table: "AnimalMedications");

            migrationBuilder.DropIndex(
                name: "UX_AnimalMedications_SyncId",
                table: "AnimalMedications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MastitisTests");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "MastitisTests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "HealthCases");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "HealthCases");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AnimalMedications");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "AnimalMedications");
        }
    }
}
