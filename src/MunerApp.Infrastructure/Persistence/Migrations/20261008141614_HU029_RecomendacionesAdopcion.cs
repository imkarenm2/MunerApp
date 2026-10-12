using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU029_RecomendacionesAdopcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigAdopcion",
                columns: table => new
                {
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Recomendaciones = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigAdopcion", x => x.EsalId);
                    table.ForeignKey(
                        name: "FK_ConfigAdopcion_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudAdopcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaAceptacionRecomendaciones = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudAdopcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudAdopcion_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudAdopcion_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudAdopcion_EsalId_Estado",
                table: "SolicitudAdopcion",
                columns: new[] { "EsalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudAdopcion_EsalId_UsuarioId",
                table: "SolicitudAdopcion",
                columns: new[] { "EsalId", "UsuarioId" },
                unique: true,
                filter: "[Estado] = N'Borrador'");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudAdopcion_UsuarioId",
                table: "SolicitudAdopcion",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigAdopcion");

            migrationBuilder.DropTable(
                name: "SolicitudAdopcion");
        }
    }
}
