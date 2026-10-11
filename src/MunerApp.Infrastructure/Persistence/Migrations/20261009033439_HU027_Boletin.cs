using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU027_Boletin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Publicacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Resumen = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Contenido = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImagenRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FechaEvento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LugarEvento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CausaId = table.Column<int>(type: "int", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadaPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publicacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publicacion_Causa_CausaId",
                        column: x => x.CausaId,
                        principalTable: "Causa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Publicacion_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Publicacion_CausaId",
                table: "Publicacion",
                column: "CausaId");

            migrationBuilder.CreateIndex(
                name: "IX_Publicacion_EsalId_Estado_FechaPublicacion",
                table: "Publicacion",
                columns: new[] { "EsalId", "Estado", "FechaPublicacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicacion_Estado_Categoria_FechaEvento",
                table: "Publicacion",
                columns: new[] { "Estado", "Categoria", "FechaEvento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Publicacion");
        }
    }
}
