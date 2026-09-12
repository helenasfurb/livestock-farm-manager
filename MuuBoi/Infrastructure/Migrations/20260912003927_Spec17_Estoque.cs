using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec17_Estoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StockCategoryId = table.Column<int>(type: "int", nullable: false),
                    UnitOfMeasureId = table.Column<int>(type: "int", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    ReplenishmentLeadDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockItems_StockCategories_StockCategoryId",
                        column: x => x.StockCategoryId,
                        principalTable: "StockCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockItems_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    MovementReason = table.Column<int>(type: "int", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    ValueEntryMode = table.Column<int>(type: "int", nullable: true),
                    UnitCostSnapshot = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "StockCategories",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Concentrado", null },
                    { 2, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Volumoso", null },
                    { 3, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Minerais e Suplementos", null },
                    { 4, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Higiene e Limpeza", null },
                    { 5, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Combustível e Lubrificantes", null },
                    { 6, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Manutenção e Ferramentas", null },
                    { 7, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Insumos Agrícolas", null },
                    { 8, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Outro", null }
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Abbreviation", "CreatedAt", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "kg", new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Quilograma", null },
                    { 2, "L", new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Litro", null },
                    { 3, null, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Bola", null },
                    { 4, null, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Fardo", null },
                    { 5, null, new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Saco", null },
                    { 6, "t", new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tonelada", null },
                    { 7, "un", new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc), true, "Unidade", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_PropertyId_IsActive",
                table: "StockItems",
                columns: new[] { "PropertyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_PropertyId_StockCategoryId",
                table: "StockItems",
                columns: new[] { "PropertyId", "StockCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_StockCategoryId",
                table: "StockItems",
                column: "StockCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_UnitOfMeasureId",
                table: "StockItems",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PropertyId_IsActive",
                table: "StockMovements",
                columns: new[] { "PropertyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PropertyId_MovementReason_MovementDate",
                table: "StockMovements",
                columns: new[] { "PropertyId", "MovementReason", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockItemId_MovementDate",
                table: "StockMovements",
                columns: new[] { "StockItemId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockItemId_MovementType_IsActive",
                table: "StockMovements",
                columns: new[] { "StockItemId", "MovementType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockItems");

            migrationBuilder.DropTable(
                name: "StockCategories");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");
        }
    }
}
