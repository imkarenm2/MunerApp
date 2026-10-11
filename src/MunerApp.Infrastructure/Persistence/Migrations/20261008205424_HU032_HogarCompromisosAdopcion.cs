using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU032_HogarCompromisosAdopcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AceptaContrato",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AceptaRequisito",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AceptaVisita",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ArrendadorPermiteMascotas",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutorizaDatos",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "SolicitudAdopcion",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Convivientes",
                table: "SolicitudAdopcion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConvivientesDeAcuerdo",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmbarazoEnHogar",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAutorizacionDatos",
                table: "SolicitudAdopcion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEnvio",
                table: "SolicitudAdopcion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NinosEnCasa",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NinosInteractuanMascotas",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PuedeCubrirCostos",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenenciaVivienda",
                table: "SolicitudAdopcion",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoVivienda",
                table: "SolicitudAdopcion",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagenToxoplasmosisRuta",
                table: "ConfigAdopcion",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MensajeToxoplasmosis",
                table: "ConfigAdopcion",
                type: "nvarchar(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAporte",
                table: "ConfigAdopcion",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 100000m);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudAdopcion_Codigo",
                table: "SolicitudAdopcion",
                column: "Codigo",
                unique: true,
                filter: "[Codigo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SolicitudAdopcion_Codigo",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "AceptaContrato",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "AceptaRequisito",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "AceptaVisita",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ArrendadorPermiteMascotas",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "AutorizaDatos",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Convivientes",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ConvivientesDeAcuerdo",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "EmbarazoEnHogar",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "FechaAutorizacionDatos",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "FechaEnvio",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "NinosEnCasa",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "NinosInteractuanMascotas",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "PuedeCubrirCostos",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "TenenciaVivienda",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "TipoVivienda",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ImagenToxoplasmosisRuta",
                table: "ConfigAdopcion");

            migrationBuilder.DropColumn(
                name: "MensajeToxoplasmosis",
                table: "ConfigAdopcion");

            migrationBuilder.DropColumn(
                name: "ValorAporte",
                table: "ConfigAdopcion");
        }
    }
}
