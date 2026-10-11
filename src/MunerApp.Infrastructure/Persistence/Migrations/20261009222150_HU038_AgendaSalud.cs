using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU038_AgendaSalud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventoAgenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaProgramada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MedicamentoId = table.Column<int>(type: "int", nullable: true),
                    Dosis = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SerieId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumeroDosis = table.Column<int>(type: "int", nullable: false),
                    TotalDosis = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRealizado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RealizadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EventoClinicoId = table.Column<int>(type: "int", nullable: true),
                    FechaCancelado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceladoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoAgenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoAgenda_Beneficiario_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventoAgenda_EventoClinico_EventoClinicoId",
                        column: x => x.EventoClinicoId,
                        principalTable: "EventoClinico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoAgenda_Medicamento_MedicamentoId",
                        column: x => x.MedicamentoId,
                        principalTable: "Medicamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventoAgenda_BeneficiarioId_FechaProgramada",
                table: "EventoAgenda",
                columns: new[] { "BeneficiarioId", "FechaProgramada" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoAgenda_EsalId_Estado_FechaProgramada",
                table: "EventoAgenda",
                columns: new[] { "EsalId", "Estado", "FechaProgramada" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoAgenda_EventoClinicoId",
                table: "EventoAgenda",
                column: "EventoClinicoId",
                unique: true,
                filter: "[EventoClinicoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventoAgenda_MedicamentoId",
                table: "EventoAgenda",
                column: "MedicamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoAgenda_SerieId",
                table: "EventoAgenda",
                column: "SerieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoAgenda");
        }
    }
}
