using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeightRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Weight = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observations = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeightRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_AnimalId",
                table: "WeightRecords",
                column: "AnimalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeightRecords");
        }
    }
}
