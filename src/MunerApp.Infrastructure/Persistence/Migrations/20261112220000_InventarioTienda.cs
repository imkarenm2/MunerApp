using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Inventario de la tienda: cantidad disponible por producto (opcional) y el historial de movimientos.
    /// Los productos que ya existen quedan sin inventario (Cantidad null) y siguen funcionando como antes.
    /// </summary>
    public partial class InventarioTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Cantidad",
                table: "Producto",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MovimientoProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    CantidadResultante = table.Column<int>(type: "int", nullable: false),
                    PedidoId = table.Column<int>(type: "int", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistradoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientoProducto_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientoProducto_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoProducto_PedidoId",
                table: "MovimientoProducto",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoProducto_ProductoId_Fecha",
                table: "MovimientoProducto",
                columns: new[] { "ProductoId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientoProducto");

            migrationBuilder.DropColumn(
                name: "Cantidad",
                table: "Producto");
        }
    }
}
