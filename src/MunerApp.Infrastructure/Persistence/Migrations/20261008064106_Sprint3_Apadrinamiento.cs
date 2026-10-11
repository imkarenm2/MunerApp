using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_Apadrinamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Apadrinable",
                table: "Beneficiario",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AporteSugerido",
                table: "Beneficiario",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaApadrinable",
                table: "Beneficiario",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoPublicaRuta",
                table: "Beneficiario",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoriaPublica",
                table: "Beneficiario",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiario_EsalId_Apadrinable",
                table: "Beneficiario",
                columns: new[] { "EsalId", "Apadrinable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiario_EsalId_Apadrinable",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Apadrinable",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "AporteSugerido",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "FechaApadrinable",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "FotoPublicaRuta",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "HistoriaPublica",
                table: "Beneficiario");
        }
    }
}
