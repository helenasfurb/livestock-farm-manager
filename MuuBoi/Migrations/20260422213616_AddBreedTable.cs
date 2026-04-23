using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Migrations
{
    /// <inheritdoc />
    public partial class AddBreedTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Breed",
                table: "Animals");

            migrationBuilder.AddColumn<int>(
                name: "BreedId",
                table: "Animals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Breeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Breeds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Animals_BreedId",
                table: "Animals",
                column: "BreedId");

            migrationBuilder.AddForeignKey(
                name: "FK_Animals_Breeds_BreedId",
                table: "Animals",
                column: "BreedId",
                principalTable: "Breeds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Animals_Breeds_BreedId",
                table: "Animals");

            migrationBuilder.DropTable(
                name: "Breeds");

            migrationBuilder.DropIndex(
                name: "IX_Animals_BreedId",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "BreedId",
                table: "Animals");

            migrationBuilder.AddColumn<string>(
                name: "Breed",
                table: "Animals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
