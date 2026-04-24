using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseEntityToBreed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Breeds",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Breeds",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Breeds",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Breeds");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Breeds");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Breeds");
        }
    }
}
