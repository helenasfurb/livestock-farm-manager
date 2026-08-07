using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyAndTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Breeds_AspNetUsers_UserId",
                table: "Breeds");

            migrationBuilder.DropForeignKey(
                name: "FK_Medications_AspNetUsers_UserId",
                table: "Medications");

            migrationBuilder.DropForeignKey(
                name: "FK_Vaccines_AspNetUsers_UserId",
                table: "Vaccines");

            migrationBuilder.DropIndex(
                name: "IX_Vaccines_UserId",
                table: "Vaccines");

            migrationBuilder.DropIndex(
                name: "IX_Medications_UserId",
                table: "Medications");

            migrationBuilder.DropIndex(
                name: "IX_Breeds_UserId",
                table: "Breeds");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Vaccines");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Breeds");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Animals");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "AspNetUsers",
                newName: "Name");

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "WeightRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Vaccines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Medications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Breeds",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "AnimalVaccinations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Animals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "AnimalMedications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_PropertyId",
                table: "WeightRecords",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Vaccines_PropertyId",
                table: "Vaccines",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_PropertyId",
                table: "Medications",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Breeds_PropertyId",
                table: "Breeds",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PropertyId",
                table: "AspNetUsers",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalVaccinations_PropertyId",
                table: "AnimalVaccinations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Animals_PropertyId",
                table: "Animals",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalMedications_PropertyId",
                table: "AnimalMedications",
                column: "PropertyId");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Properties WHERE Id = '00000000-0000-0000-0000-000000000000')
                BEGIN
                    INSERT INTO Properties (Id, Name, CreatedAt)
                    VALUES ('00000000-0000-0000-0000-000000000000', 'Legado', GETUTCDATE())
                END
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Properties_PropertyId",
                table: "AspNetUsers",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Properties_PropertyId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_WeightRecords_PropertyId",
                table: "WeightRecords");

            migrationBuilder.DropIndex(
                name: "IX_Vaccines_PropertyId",
                table: "Vaccines");

            migrationBuilder.DropIndex(
                name: "IX_Medications_PropertyId",
                table: "Medications");

            migrationBuilder.DropIndex(
                name: "IX_Breeds_PropertyId",
                table: "Breeds");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PropertyId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AnimalVaccinations_PropertyId",
                table: "AnimalVaccinations");

            migrationBuilder.DropIndex(
                name: "IX_Animals_PropertyId",
                table: "Animals");

            migrationBuilder.DropIndex(
                name: "IX_AnimalMedications_PropertyId",
                table: "AnimalMedications");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "WeightRecords");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Vaccines");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Breeds");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "AnimalVaccinations");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "AnimalMedications");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AspNetUsers",
                newName: "FullName");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Vaccines",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Medications",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Breeds",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Animals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Vaccines_UserId",
                table: "Vaccines",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_UserId",
                table: "Medications",
                column: "UserId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Medications_AspNetUsers_UserId",
                table: "Medications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Vaccines_AspNetUsers_UserId",
                table: "Vaccines",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
