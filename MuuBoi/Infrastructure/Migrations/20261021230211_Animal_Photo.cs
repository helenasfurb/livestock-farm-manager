using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Animal_Photo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoHash",
                table: "Animals",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhotoUpdatedAt",
                table: "Animals",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoHash",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "PhotoUpdatedAt",
                table: "Animals");
        }
    }
}
