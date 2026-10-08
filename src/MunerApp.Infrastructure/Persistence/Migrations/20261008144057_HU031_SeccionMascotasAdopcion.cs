using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU031_SeccionMascotasAdopcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarneVacunasRuta",
                table: "SolicitudAdopcion",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatoEsterilizacion",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GatoUsaArenero",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatoVacunas",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mascotas",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtraMascota",
                table: "SolicitudAdopcion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerroSociabilidad",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuePasoMascota",
                table: "SolicitudAdopcion",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TieneGato",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TieneOtraMascota",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TienePerro",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarneVacunasRuta",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "GatoEsterilizacion",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "GatoUsaArenero",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "GatoVacunas",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Mascotas",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "OtraMascota",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "PerroSociabilidad",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "QuePasoMascota",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "TieneGato",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "TieneOtraMascota",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "TienePerro",
                table: "SolicitudAdopcion");
        }
    }
}
