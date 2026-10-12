using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU025_ChatTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversacionTienda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    DonanteId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaUltimoMensaje = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaLecturaDonante = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaLecturaFundacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversacionTienda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversacionTienda_AspNetUsers_DonanteId",
                        column: x => x.DonanteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversacionTienda_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversacionTienda_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MensajeTienda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    ConversacionId = table.Column<int>(type: "int", nullable: false),
                    AutorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DeLaFundacion = table.Column<bool>(type: "bit", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajeTienda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensajeTienda_ConversacionTienda_ConversacionId",
                        column: x => x.ConversacionId,
                        principalTable: "ConversacionTienda",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionTienda_DonanteId_ProductoId",
                table: "ConversacionTienda",
                columns: new[] { "DonanteId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionTienda_EsalId_Estado_FechaUltimoMensaje",
                table: "ConversacionTienda",
                columns: new[] { "EsalId", "Estado", "FechaUltimoMensaje" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionTienda_ProductoId",
                table: "ConversacionTienda",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MensajeTienda_ConversacionId_Id",
                table: "MensajeTienda",
                columns: new[] { "ConversacionId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensajeTienda");

            migrationBuilder.DropTable(
                name: "ConversacionTienda");
        }
    }
}
