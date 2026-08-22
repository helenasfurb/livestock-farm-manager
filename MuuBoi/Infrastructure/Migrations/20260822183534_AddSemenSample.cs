using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSemenSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SemenSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BullRegistration = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GeneticsCompany = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BullBreed = table.Column<int>(type: "int", nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManufacturedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemenSamples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SemenSamples_PropertyId_IsActive",
                table: "SemenSamples",
                columns: new[] { "PropertyId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemenSamples");
        }
    }
}
