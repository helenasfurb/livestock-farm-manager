using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec_ControleSanitario_Vacinacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaccinationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccineId = table.Column<int>(type: "int", nullable: false),
                    DoseType = table.Column<int>(type: "int", nullable: false),
                    PredictedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApplicationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentEventId = table.Column<int>(type: "int", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaccinationEvents_VaccinationEvents_ParentEventId",
                        column: x => x.ParentEventId,
                        principalTable: "VaccinationEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaccinationEvents_Vaccines_VaccineId",
                        column: x => x.VaccineId,
                        principalTable: "Vaccines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationEventAnimals",
                columns: table => new
                {
                    VaccinationEventId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationEventAnimals", x => new { x.VaccinationEventId, x.AnimalId });
                    table.ForeignKey(
                        name: "FK_VaccinationEventAnimals_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaccinationEventAnimals_VaccinationEvents_VaccinationEventId",
                        column: x => x.VaccinationEventId,
                        principalTable: "VaccinationEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEventAnimals_AnimalId",
                table: "VaccinationEventAnimals",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_PropertyId",
                table: "VaccinationEvents",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_PropertyId_ApplicationDate",
                table: "VaccinationEvents",
                columns: new[] { "PropertyId", "ApplicationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_PropertyId_VaccineId",
                table: "VaccinationEvents",
                columns: new[] { "PropertyId", "VaccineId" });

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_VaccineId",
                table: "VaccinationEvents",
                column: "VaccineId");

            migrationBuilder.CreateIndex(
                name: "UX_VaccinationEvents_ParentEventId_Active",
                table: "VaccinationEvents",
                column: "ParentEventId",
                unique: true,
                filter: "[ParentEventId] IS NOT NULL AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaccinationEventAnimals");

            migrationBuilder.DropTable(
                name: "VaccinationEvents");
        }
    }
}
