using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec15_DiagnosisMethodAndGestationalAge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiagnosisMethod",
                table: "BreedingEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GestationalAge",
                table: "AnimalPregnancies",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagnosisMethod",
                table: "BreedingEvents");

            migrationBuilder.DropColumn(
                name: "GestationalAge",
                table: "AnimalPregnancies");
        }
    }
}
