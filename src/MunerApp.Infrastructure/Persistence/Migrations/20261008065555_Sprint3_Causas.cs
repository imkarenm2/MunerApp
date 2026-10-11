using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_Causas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CausaId",
                table: "Donacion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Causa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Meta = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    FechaLimite = table.Column<DateTime>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadaPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RendicionDocumentoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Causa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Causa_DocumentoTransparencia_RendicionDocumentoId",
                        column: x => x.RendicionDocumentoId,
                        principalTable: "DocumentoTransparencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Causa_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FotoCausa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    CausaId = table.Column<int>(type: "int", nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotoCausa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotoCausa_Causa_CausaId",
                        column: x => x.CausaId,
                        principalTable: "Causa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_CausaId",
                table: "Donacion",
                column: "CausaId");

            migrationBuilder.CreateIndex(
                name: "IX_Causa_EsalId_Estado",
                table: "Causa",
                columns: new[] { "EsalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Causa_RendicionDocumentoId",
                table: "Causa",
                column: "RendicionDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FotoCausa_CausaId",
                table: "FotoCausa",
                column: "CausaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Donacion_Causa_CausaId",
                table: "Donacion",
                column: "CausaId",
                principalTable: "Causa",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Donacion_Causa_CausaId",
                table: "Donacion");

            migrationBuilder.DropTable(
                name: "FotoCausa");

            migrationBuilder.DropTable(
                name: "Causa");

            migrationBuilder.DropIndex(
                name: "IX_Donacion_CausaId",
                table: "Donacion");

            migrationBuilder.DropColumn(
                name: "CausaId",
                table: "Donacion");
        }
    }
}
