using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Libro de fondos (asignación, movimientos y destinación del dinero) y salidas de la especie (destinación de cada lote).
    /// Solo crea tablas nuevas: no toca datos existentes.
    /// </summary>
    public partial class LibroDeFondos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fondo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CausaId = table.Column<int>(type: "int", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fondo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fondo_Causa_CausaId",
                        column: x => x.CausaId,
                        principalTable: "Causa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fondo_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalidaEspecie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    DonacionEspecieId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Uso = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FotoRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RegistradoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Anulado = table.Column<bool>(type: "bit", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AnuladoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalidaEspecie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalidaEspecie_DonacionEspecie_DonacionEspecieId",
                        column: x => x.DonacionEspecieId,
                        principalTable: "DonacionEspecie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientoFondo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    FondoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Proveedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SoporteRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FondoRelacionadoId = table.Column<int>(type: "int", nullable: true),
                    MovimientoRelacionadoId = table.Column<int>(type: "int", nullable: true),
                    RegistradoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Anulado = table.Column<bool>(type: "bit", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AnuladoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoFondo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientoFondo_Fondo_FondoId",
                        column: x => x.FondoId,
                        principalTable: "Fondo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientoFondo_Fondo_FondoRelacionadoId",
                        column: x => x.FondoRelacionadoId,
                        principalTable: "Fondo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fondo_CausaId",
                table: "Fondo",
                column: "CausaId",
                unique: true,
                filter: "[CausaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Fondo_EsalId_Tipo",
                table: "Fondo",
                columns: new[] { "EsalId", "Tipo" },
                unique: true,
                filter: "[Tipo] IN (N'General', N'Apadrinamiento')");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoFondo_EsalId_Fecha",
                table: "MovimientoFondo",
                columns: new[] { "EsalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoFondo_FondoId_Fecha",
                table: "MovimientoFondo",
                columns: new[] { "FondoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoFondo_FondoRelacionadoId",
                table: "MovimientoFondo",
                column: "FondoRelacionadoId");

            migrationBuilder.CreateIndex(
                name: "IX_SalidaEspecie_DonacionEspecieId_Fecha",
                table: "SalidaEspecie",
                columns: new[] { "DonacionEspecieId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientoFondo");

            migrationBuilder.DropTable(
                name: "SalidaEspecie");

            migrationBuilder.DropTable(
                name: "Fondo");
        }
    }
}
