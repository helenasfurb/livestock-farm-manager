using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec11_2_Lactation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lactations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalvingId = table.Column<int>(type: "int", nullable: true),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    DryOffNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lactations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lactations_AnimalCalvings_CalvingId",
                        column: x => x.CalvingId,
                        principalTable: "AnimalCalvings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lactations_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_AnimalId",
                table: "Lactations",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_CalvingId",
                table: "Lactations",
                column: "CalvingId");

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_PropertyId",
                table: "Lactations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_PropertyId_AnimalId_EndDate",
                table: "Lactations",
                columns: new[] { "PropertyId", "AnimalId", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lactations");
        }
    }
}
