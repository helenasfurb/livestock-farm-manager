using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Offline_BodyConditionRecord_PropertyId_SyncId_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "BodyConditionRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE r SET r.[PropertyId] = a.[PropertyId]
                FROM [BodyConditionRecords] r
                INNER JOIN [Animals] a ON a.[Id] = r.[AnimalId];");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "BodyConditionRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "BodyConditionRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_BodyConditionRecords_PropertyId",
                table: "BodyConditionRecords",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_BodyConditionRecords_PropertyId_RowVersion",
                table: "BodyConditionRecords",
                columns: new[] { "PropertyId", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_BodyConditionRecords_SyncId",
                table: "BodyConditionRecords",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BodyConditionRecords_PropertyId",
                table: "BodyConditionRecords");

            migrationBuilder.DropIndex(
                name: "IX_BodyConditionRecords_PropertyId_RowVersion",
                table: "BodyConditionRecords");

            migrationBuilder.DropIndex(
                name: "UX_BodyConditionRecords_SyncId",
                table: "BodyConditionRecords");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "BodyConditionRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BodyConditionRecords");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "BodyConditionRecords");
        }
    }
}
