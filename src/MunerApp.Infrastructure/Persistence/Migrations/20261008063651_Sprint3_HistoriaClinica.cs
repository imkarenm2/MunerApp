using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_HistoriaClinica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventoClinico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RegistradoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoClinico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoClinico_Beneficiario_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FotoEventoClinico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    EventoClinicoId = table.Column<int>(type: "int", nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotoEventoClinico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotoEventoClinico_EventoClinico_EventoClinicoId",
                        column: x => x.EventoClinicoId,
                        principalTable: "EventoClinico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventoClinico_BeneficiarioId_Fecha",
                table: "EventoClinico",
                columns: new[] { "BeneficiarioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_FotoEventoClinico_EventoClinicoId",
                table: "FotoEventoClinico",
                column: "EventoClinicoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotoEventoClinico");

            migrationBuilder.DropTable(
                name: "EventoClinico");
        }
    }
}
