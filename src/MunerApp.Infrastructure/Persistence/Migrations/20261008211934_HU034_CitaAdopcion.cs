using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU034_CitaAdopcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BeneficiarioAdoptadoId",
                table: "SolicitudAdopcion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCita",
                table: "SolicitudAdopcion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaResultado",
                table: "SolicitudAdopcion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicacionesCita",
                table: "SolicitudAdopcion",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LugarCita",
                table: "SolicitudAdopcion",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesResultado",
                table: "SolicitudAdopcion",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Reprogramaciones",
                table: "SolicitudAdopcion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudAdopcion_BeneficiarioAdoptadoId",
                table: "SolicitudAdopcion",
                column: "BeneficiarioAdoptadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudAdopcion_Beneficiario_BeneficiarioAdoptadoId",
                table: "SolicitudAdopcion",
                column: "BeneficiarioAdoptadoId",
                principalTable: "Beneficiario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudAdopcion_Beneficiario_BeneficiarioAdoptadoId",
                table: "SolicitudAdopcion");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudAdopcion_BeneficiarioAdoptadoId",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "BeneficiarioAdoptadoId",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "FechaCita",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "FechaResultado",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "IndicacionesCita",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "LugarCita",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ObservacionesResultado",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Reprogramaciones",
                table: "SolicitudAdopcion");
        }
    }
}
