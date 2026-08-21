using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec2_AddAnimalExitRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeathCause",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "ExitDate",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "ExitNotes",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "ExitReason",
                table: "Animals");

            migrationBuilder.CreateTable(
                name: "AnimalExitRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    ExitReason = table.Column<int>(type: "int", nullable: false),
                    ExitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalExitRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalExitRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalExitRecords_AnimalId_ExitDate",
                table: "AnimalExitRecords",
                columns: new[] { "AnimalId", "ExitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalExitRecords_ExitDate_ExitReason",
                table: "AnimalExitRecords",
                columns: new[] { "ExitDate", "ExitReason" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalExitRecords");

            migrationBuilder.AddColumn<int>(
                name: "DeathCause",
                table: "Animals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitDate",
                table: "Animals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitNotes",
                table: "Animals",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExitReason",
                table: "Animals",
                type: "int",
                nullable: true);
        }
    }
}
