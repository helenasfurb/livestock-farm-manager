using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec11_1_MilkProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilkProductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Milking = table.Column<int>(type: "int", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(11,2)", precision: 11, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkProductions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilkProductions_PropertyId",
                table: "MilkProductions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkProductions_PropertyId_Date",
                table: "MilkProductions",
                columns: new[] { "PropertyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkProductions");
        }
    }
}
