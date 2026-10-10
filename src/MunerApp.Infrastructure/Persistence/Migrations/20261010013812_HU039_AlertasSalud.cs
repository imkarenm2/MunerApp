using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU039_AlertasSalud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VencimientoAvisado",
                table: "Medicamento",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecordatorioEnviado",
                table: "EventoAgenda",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConfigSalud",
                columns: table => new
                {
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    DiasAvisoVencimiento = table.Column<int>(type: "int", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigSalud", x => x.EsalId);
                    table.ForeignKey(
                        name: "FK_ConfigSalud_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevisionDiaria",
                columns: table => new
                {
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Tarea = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Iniciada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Terminada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resumen = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionDiaria", x => new { x.Fecha, x.Tarea });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigSalud");

            migrationBuilder.DropTable(
                name: "RevisionDiaria");

            migrationBuilder.DropColumn(
                name: "VencimientoAvisado",
                table: "Medicamento");

            migrationBuilder.DropColumn(
                name: "RecordatorioEnviado",
                table: "EventoAgenda");
        }
    }
}
