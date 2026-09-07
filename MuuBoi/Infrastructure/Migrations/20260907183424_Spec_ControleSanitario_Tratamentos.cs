using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec_ControleSanitario_Tratamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnimalMedications_Medications_MedicationId",
                table: "AnimalMedications");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "AnimalMedications",
                newName: "ApplicationDate");

            migrationBuilder.AlterColumn<int>(
                name: "MedicationId",
                table: "AnimalMedications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "HealthCaseId",
                table: "AnimalMedications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicationName",
                table: "AnimalMedications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HealthCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    DiseaseType = table.Column<int>(type: "int", nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiagnosisDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AffectedQuarters = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthCases_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MastitisTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HealthCaseId = table.Column<int>(type: "int", nullable: false),
                    TestType = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MastitisTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MastitisTests_HealthCases_HealthCaseId",
                        column: x => x.HealthCaseId,
                        principalTable: "HealthCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalMedications_HealthCaseId",
                table: "AnimalMedications",
                column: "HealthCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthCases_AnimalId",
                table: "HealthCases",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthCases_PropertyId",
                table: "HealthCases",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthCases_PropertyId_AnimalId",
                table: "HealthCases",
                columns: new[] { "PropertyId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthCases_PropertyId_DiagnosisDate",
                table: "HealthCases",
                columns: new[] { "PropertyId", "DiagnosisDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthCases_PropertyId_DiseaseType",
                table: "HealthCases",
                columns: new[] { "PropertyId", "DiseaseType" });

            migrationBuilder.CreateIndex(
                name: "IX_MastitisTests_HealthCaseId",
                table: "MastitisTests",
                column: "HealthCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MastitisTests_PropertyId",
                table: "MastitisTests",
                column: "PropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalMedications_HealthCases_HealthCaseId",
                table: "AnimalMedications",
                column: "HealthCaseId",
                principalTable: "HealthCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalMedications_Medications_MedicationId",
                table: "AnimalMedications",
                column: "MedicationId",
                principalTable: "Medications",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnimalMedications_HealthCases_HealthCaseId",
                table: "AnimalMedications");

            migrationBuilder.DropForeignKey(
                name: "FK_AnimalMedications_Medications_MedicationId",
                table: "AnimalMedications");

            migrationBuilder.DropTable(
                name: "MastitisTests");

            migrationBuilder.DropTable(
                name: "HealthCases");

            migrationBuilder.DropIndex(
                name: "IX_AnimalMedications_HealthCaseId",
                table: "AnimalMedications");

            migrationBuilder.DropColumn(
                name: "HealthCaseId",
                table: "AnimalMedications");

            migrationBuilder.DropColumn(
                name: "MedicationName",
                table: "AnimalMedications");

            migrationBuilder.RenameColumn(
                name: "ApplicationDate",
                table: "AnimalMedications",
                newName: "StartDate");

            migrationBuilder.AlterColumn<int>(
                name: "MedicationId",
                table: "AnimalMedications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalMedications_Medications_MedicationId",
                table: "AnimalMedications",
                column: "MedicationId",
                principalTable: "Medications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
