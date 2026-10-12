using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU030_DatosPersonalesAdopcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cedula",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Celular",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ciudad",
                table: "SolicitudAdopcion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalleOcupacion",
                table: "SolicitudAdopcion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "SolicitudAdopcion",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Edad",
                table: "SolicitudAdopcion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreCompleto",
                table: "SolicitudAdopcion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ocupacion",
                table: "SolicitudAdopcion",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaCelular",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaNombre",
                table: "SolicitudAdopcion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaRelacion",
                table: "SolicitudAdopcion",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeccionesCompletadas",
                table: "SolicitudAdopcion",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cedula",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Celular",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Ciudad",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "DetalleOcupacion",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Edad",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "NombreCompleto",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Ocupacion",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ReferenciaCelular",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ReferenciaNombre",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ReferenciaRelacion",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "SeccionesCompletadas",
                table: "SolicitudAdopcion");
        }
    }
}
