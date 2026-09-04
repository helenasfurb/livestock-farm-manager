using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Deprecate_AnimalVaccination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalVaccinations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimalVaccinations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    VaccineId = table.Column<int>(type: "int", nullable: false),
                    ApplicationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DosageMl = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NextApplicationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Observations = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Responsible = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalVaccinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalVaccinations_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnimalVaccinations_Vaccines_VaccineId",
                        column: x => x.VaccineId,
                        principalTable: "Vaccines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalVaccinations_AnimalId",
                table: "AnimalVaccinations",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalVaccinations_PropertyId",
                table: "AnimalVaccinations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalVaccinations_VaccineId",
                table: "AnimalVaccinations",
                column: "VaccineId");
        }
    }
}
