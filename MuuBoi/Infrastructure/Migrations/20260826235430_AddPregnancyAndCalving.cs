using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPregnancyAndCalving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimalPregnancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    BreedingEventId = table.Column<int>(type: "int", nullable: false),
                    ConfirmationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedCalvingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LossDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalPregnancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalPregnancies_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalPregnancies_BreedingEvents_BreedingEventId",
                        column: x => x.BreedingEventId,
                        principalTable: "BreedingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalCalvings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalPregnancyId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    CalvingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalCalvings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalCalvings_AnimalPregnancies_AnimalPregnancyId",
                        column: x => x.AnimalPregnancyId,
                        principalTable: "AnimalPregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalCalvings_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalCalvingCalves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalvingId = table.Column<int>(type: "int", nullable: false),
                    Sex = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    VitalStatus = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalCalvingCalves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalCalvingCalves_AnimalCalvings_CalvingId",
                        column: x => x.CalvingId,
                        principalTable: "AnimalCalvings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCalvingCalves_CalvingId",
                table: "AnimalCalvingCalves",
                column: "CalvingId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCalvings_AnimalId_CalvingDate",
                table: "AnimalCalvings",
                columns: new[] { "AnimalId", "CalvingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCalvings_AnimalPregnancyId",
                table: "AnimalCalvings",
                column: "AnimalPregnancyId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_AnimalId_Status_IsActive",
                table: "AnimalPregnancies",
                columns: new[] { "AnimalId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_BreedingEventId",
                table: "AnimalPregnancies",
                column: "BreedingEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_PropertyId_IsActive",
                table: "AnimalPregnancies",
                columns: new[] { "PropertyId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalCalvingCalves");

            migrationBuilder.DropTable(
                name: "AnimalCalvings");

            migrationBuilder.DropTable(
                name: "AnimalPregnancies");
        }
    }
}
