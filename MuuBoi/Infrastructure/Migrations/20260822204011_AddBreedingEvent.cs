using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBreedingEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BreedingEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    ReproductionType = table.Column<int>(type: "int", nullable: false),
                    BreedingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SemenSampleId = table.Column<int>(type: "int", nullable: true),
                    SireAnimalId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DiagnosisDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreedingEvents_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingEvents_Animals_SireAnimalId",
                        column: x => x.SireAnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingEvents_SemenSamples_SemenSampleId",
                        column: x => x.SemenSampleId,
                        principalTable: "SemenSamples",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_AnimalId_BreedingDate",
                table: "BreedingEvents",
                columns: new[] { "AnimalId", "BreedingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_AnimalId_Status_IsActive",
                table: "BreedingEvents",
                columns: new[] { "AnimalId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_SemenSampleId",
                table: "BreedingEvents",
                column: "SemenSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_SireAnimalId",
                table: "BreedingEvents",
                column: "SireAnimalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreedingEvents");
        }
    }
}
