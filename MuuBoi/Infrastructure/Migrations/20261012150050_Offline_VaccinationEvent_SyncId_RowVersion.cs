using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_VaccinationEvent_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "VaccinationEvents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "VaccinationEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_PropertyId_RowVersion",
                table: "VaccinationEvents",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_VaccinationEvents_SyncId",
                table: "VaccinationEvents",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VaccinationEvents_PropertyId_RowVersion",
                table: "VaccinationEvents");

            migrationBuilder.DropIndex(
                name: "UX_VaccinationEvents_SyncId",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "VaccinationEvents");
        }
    }
}
