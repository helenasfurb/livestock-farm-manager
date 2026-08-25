using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec8_SemenSampleMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BullName",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "ManufacturedAt",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "SemenSamples");

            migrationBuilder.AddColumn<DateTime>(
                name: "BatchDate",
                table: "SemenSamples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "SemenSamples",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SemenSampleMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SemenSampleId = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BreedingEventId = table.Column<int>(type: "int", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemenSampleMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemenSampleMovements_BreedingEvents_BreedingEventId",
                        column: x => x.BreedingEventId,
                        principalTable: "BreedingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SemenSampleMovements_SemenSamples_SemenSampleId",
                        column: x => x.SemenSampleId,
                        principalTable: "SemenSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SemenSampleMovements_BreedingEventId",
                table: "SemenSampleMovements",
                column: "BreedingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SemenSampleMovements_PropertyId_IsActive",
                table: "SemenSampleMovements",
                columns: new[] { "PropertyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SemenSampleMovements_SemenSampleId_MovementType_IsActive",
                table: "SemenSampleMovements",
                columns: new[] { "SemenSampleId", "MovementType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemenSampleMovements");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "SemenSamples");

            migrationBuilder.DropColumn(
                name: "BatchDate",
                table: "SemenSamples");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "SemenSamples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BullName",
                table: "SemenSamples",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAt",
                table: "SemenSamples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "SemenSamples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufacturedAt",
                table: "SemenSamples",
                type: "datetime2",
                nullable: true);
        }
    }
}
