using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU046_CierreRendicionCausas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCierre",
                table: "Causa",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCierre",
                table: "Causa",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RendicionCausa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    CausaId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValorEjecutado = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    Recaudado = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Donaciones = table.Column<int>(type: "int", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublicadaPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DocumentoId = table.Column<int>(type: "int", nullable: true),
                    PublicacionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RendicionCausa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RendicionCausa_Causa_CausaId",
                        column: x => x.CausaId,
                        principalTable: "Causa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RendicionCausa_DocumentoTransparencia_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "DocumentoTransparencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RendicionCausa_Publicacion_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchivoRendicion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    RendicionId = table.Column<int>(type: "int", nullable: false),
                    EsSoporte = table.Column<bool>(type: "bit", nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivoRendicion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivoRendicion_RendicionCausa_RendicionId",
                        column: x => x.RendicionId,
                        principalTable: "RendicionCausa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivoRendicion_RendicionId",
                table: "ArchivoRendicion",
                column: "RendicionId");

            migrationBuilder.CreateIndex(
                name: "IX_RendicionCausa_CausaId",
                table: "RendicionCausa",
                column: "CausaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RendicionCausa_DocumentoId",
                table: "RendicionCausa",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_RendicionCausa_PublicacionId",
                table: "RendicionCausa",
                column: "PublicacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivoRendicion");

            migrationBuilder.DropTable(
                name: "RendicionCausa");

            migrationBuilder.DropColumn(
                name: "FechaCierre",
                table: "Causa");

            migrationBuilder.DropColumn(
                name: "MotivoCierre",
                table: "Causa");
        }
    }
}
