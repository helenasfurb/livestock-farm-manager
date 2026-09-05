using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuuBoi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec13_RetroactivePregnancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_BreedingEventId",
                table: "AnimalPregnancies");

            migrationBuilder.AlterColumn<int>(
                name: "BreedingEventId",
                table: "AnimalPregnancies",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "AnimalPregnancies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SemenSampleId",
                table: "AnimalPregnancies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SireAnimalId",
                table: "AnimalPregnancies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_BreedingEventId",
                table: "AnimalPregnancies",
                column: "BreedingEventId",
                unique: true,
                filter: "[BreedingEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_ClientRequestId",
                table: "AnimalPregnancies",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_SemenSampleId",
                table: "AnimalPregnancies",
                column: "SemenSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_SireAnimalId",
                table: "AnimalPregnancies",
                column: "SireAnimalId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalPregnancies_Animals_SireAnimalId",
                table: "AnimalPregnancies",
                column: "SireAnimalId",
                principalTable: "Animals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnimalPregnancies_SemenSamples_SemenSampleId",
                table: "AnimalPregnancies",
                column: "SemenSampleId",
                principalTable: "SemenSamples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnimalPregnancies_Animals_SireAnimalId",
                table: "AnimalPregnancies");

            migrationBuilder.DropForeignKey(
                name: "FK_AnimalPregnancies_SemenSamples_SemenSampleId",
                table: "AnimalPregnancies");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_BreedingEventId",
                table: "AnimalPregnancies");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_ClientRequestId",
                table: "AnimalPregnancies");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_SemenSampleId",
                table: "AnimalPregnancies");

            migrationBuilder.DropIndex(
                name: "IX_AnimalPregnancies_SireAnimalId",
                table: "AnimalPregnancies");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "AnimalPregnancies");

            migrationBuilder.DropColumn(
                name: "SemenSampleId",
                table: "AnimalPregnancies");

            migrationBuilder.DropColumn(
                name: "SireAnimalId",
                table: "AnimalPregnancies");

            migrationBuilder.AlterColumn<int>(
                name: "BreedingEventId",
                table: "AnimalPregnancies",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalPregnancies_BreedingEventId",
                table: "AnimalPregnancies",
                column: "BreedingEventId",
                unique: true);
        }
    }
}
