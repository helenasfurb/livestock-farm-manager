using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Migrations
{
    /// <inheritdoc />
    public partial class AddBreedUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Breeds",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Breeds",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Breeds_UserId",
                table: "Breeds",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Breeds_AspNetUsers_UserId",
                table: "Breeds",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Breeds_AspNetUsers_UserId",
                table: "Breeds");

            migrationBuilder.DropIndex(
                name: "IX_Breeds_UserId",
                table: "Breeds");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Breeds");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Breeds",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
