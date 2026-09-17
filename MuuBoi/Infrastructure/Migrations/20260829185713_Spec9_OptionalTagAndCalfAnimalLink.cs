using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec9_OptionalTagAndCalfAnimalLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TagNumber",
                table: "Animals",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(6)",
                oldMaxLength: 6);

            migrationBuilder.AddColumn<int>(
                name: "AnimalId",
                table: "AnimalCalvingCalves",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCalvingCalves_AnimalId",
                table: "AnimalCalvingCalves",
                column: "AnimalId",
                unique: true,
                filter: "[AnimalId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalCalvingCalves_Animals_AnimalId",
                table: "AnimalCalvingCalves",
                column: "AnimalId",
                principalTable: "Animals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnimalCalvingCalves_Animals_AnimalId",
                table: "AnimalCalvingCalves");

            migrationBuilder.DropIndex(
                name: "IX_AnimalCalvingCalves_AnimalId",
                table: "AnimalCalvingCalves");

            migrationBuilder.DropColumn(
                name: "AnimalId",
                table: "AnimalCalvingCalves");

            migrationBuilder.AlterColumn<string>(
                name: "TagNumber",
                table: "Animals",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(6)",
                oldMaxLength: 6,
                oldNullable: true);
        }
    }
}
