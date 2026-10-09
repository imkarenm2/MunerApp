using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_HojaDeVidaFormato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Laboratorio",
                table: "EventoClinico",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoKg",
                table: "EventoClinico",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Producto",
                table: "EventoClinico",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resultado",
                table: "EventoClinico",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CondicionCorporal",
                table: "Beneficiario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetallesProcedencia",
                table: "Beneficiario",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Especie",
                table: "Beneficiario",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Felino");

            migrationBuilder.AddColumn<string>(
                name: "EstadoConciencia",
                table: "Beneficiario",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoReproductivo",
                table: "Beneficiario",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExamenIngresoPorId",
                table: "Beneficiario",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaExamenIngreso",
                table: "Beneficiario",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FechaNacimientoExacta",
                table: "Beneficiario",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FrecuenciaCardiaca",
                table: "Beneficiario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrecuenciaRespiratoria",
                table: "Beneficiario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MucosaConjuntival",
                table: "Beneficiario",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MucosaOral",
                table: "Beneficiario",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MucosaRectal",
                table: "Beneficiario",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MucosaVulvarPrepucial",
                table: "Beneficiario",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesIngreso",
                table: "Beneficiario",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoIngresoKg",
                table: "Beneficiario",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Procedencia",
                table: "Beneficiario",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Raza",
                table: "Beneficiario",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rpc",
                table: "Beneficiario",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenalesParticulares",
                table: "Beneficiario",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Temperatura",
                table: "Beneficiario",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Tllc",
                table: "Beneficiario",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Documento",
                table: "AdoptanteBeneficiario",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Direccion",
                table: "AdoptanteBeneficiario",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Elaboro",
                table: "AdoptanteBeneficiario",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroFormulario",
                table: "AdoptanteBeneficiario",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EvaluacionComportamiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: false),
                    Momento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Conductas = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Metodo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RegistradoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluacionComportamiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluacionComportamiento_Beneficiario_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionComportamiento_BeneficiarioId_Fecha",
                table: "EvaluacionComportamiento",
                columns: new[] { "BeneficiarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluacionComportamiento");

            migrationBuilder.DropColumn(
                name: "Laboratorio",
                table: "EventoClinico");

            migrationBuilder.DropColumn(
                name: "PesoKg",
                table: "EventoClinico");

            migrationBuilder.DropColumn(
                name: "Producto",
                table: "EventoClinico");

            migrationBuilder.DropColumn(
                name: "Resultado",
                table: "EventoClinico");

            migrationBuilder.DropColumn(
                name: "CondicionCorporal",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "DetallesProcedencia",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Especie",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "EstadoConciencia",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "EstadoReproductivo",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "ExamenIngresoPorId",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "FechaExamenIngreso",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "FechaNacimientoExacta",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "FrecuenciaCardiaca",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "FrecuenciaRespiratoria",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "MucosaConjuntival",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "MucosaOral",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "MucosaRectal",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "MucosaVulvarPrepucial",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "ObservacionesIngreso",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "PesoIngresoKg",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Procedencia",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Raza",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Rpc",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "SenalesParticulares",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Temperatura",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Tllc",
                table: "Beneficiario");

            migrationBuilder.DropColumn(
                name: "Elaboro",
                table: "AdoptanteBeneficiario");

            migrationBuilder.DropColumn(
                name: "NumeroFormulario",
                table: "AdoptanteBeneficiario");

            migrationBuilder.AlterColumn<string>(
                name: "Documento",
                table: "AdoptanteBeneficiario",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Direccion",
                table: "AdoptanteBeneficiario",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
