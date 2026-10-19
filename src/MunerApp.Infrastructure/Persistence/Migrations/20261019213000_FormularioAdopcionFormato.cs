using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Formulario de adopción ajustado al formato de El Reino de los Gatos (reunión de levantamiento del 9 de septiembre):
    /// agrega las preguntas que faltaban. Todas las columnas son opcionales para no afectar las solicitudes ya enviadas.
    /// </summary>
    public partial class FormularioAdopcionFormato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelefonoAdicional",
                table: "SolicitudAdopcion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barrio",
                table: "SolicitudAdopcion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EdadMascotas",
                table: "SolicitudAdopcion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatoSalud",
                table: "SolicitudAdopcion",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAdopcion",
                table: "SolicitudAdopcion",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatoBuscado",
                table: "SolicitudAdopcion",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CuidaraQuinceAnios",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LoLlevariaAlMudarse",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LugarParaMascota",
                table: "SolicitudAdopcion",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NinosEdades",
                table: "SolicitudAdopcion",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NinosOtro",
                table: "SolicitudAdopcion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConoceToxoplasmosis",
                table: "SolicitudAdopcion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MejorCualidadGato",
                table: "SolicitudAdopcion",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostoMensualEstimado",
                table: "SolicitudAdopcion",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comentarios",
                table: "SolicitudAdopcion",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelefonoAdicional",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Barrio",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "EdadMascotas",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "GatoSalud",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "MotivoAdopcion",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "GatoBuscado",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "CuidaraQuinceAnios",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "LoLlevariaAlMudarse",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "LugarParaMascota",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "NinosEdades",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "NinosOtro",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "ConoceToxoplasmosis",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "MejorCualidadGato",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "CostoMensualEstimado",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "Comentarios",
                table: "SolicitudAdopcion");
        }
    }
}
