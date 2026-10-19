using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <summary>Donaciones en especie (necesidades de la fundación y lo que ofrecen los donantes) y cuentas de veeduría ciudadana.</summary>
    public partial class DonacionesEspecieVeeduria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Organizacion",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NecesidadEspecie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CantidadMeta = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Urgente = table.Column<bool>(type: "bit", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadaPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NecesidadEspecie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NecesidadEspecie_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonacionEspecie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DonanteId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NecesidadId = table.Column<int>(type: "int", nullable: true),
                    Articulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CantidadOfrecida = table.Column<int>(type: "int", nullable: false),
                    FormaEntrega = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    FechaEntregaEstimada = table.Column<DateTime>(type: "date", nullable: false),
                    DireccionRecogida = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    FechaOferta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CantidadRecibida = table.Column<int>(type: "int", nullable: true),
                    FechaRecepcion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaVencimiento = table.Column<DateTime>(type: "date", nullable: true),
                    ObservacionesRecepcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FotoRecepcionRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecibidaPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MotivoNoRecibida = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonacionEspecie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonacionEspecie_AspNetUsers_DonanteId",
                        column: x => x.DonanteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonacionEspecie_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonacionEspecie_NecesidadEspecie_NecesidadId",
                        column: x => x.NecesidadId,
                        principalTable: "NecesidadEspecie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonacionEspecie_Codigo",
                table: "DonacionEspecie",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonacionEspecie_DonanteId",
                table: "DonacionEspecie",
                column: "DonanteId");

            migrationBuilder.CreateIndex(
                name: "IX_DonacionEspecie_EsalId_Estado",
                table: "DonacionEspecie",
                columns: new[] { "EsalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_DonacionEspecie_NecesidadId",
                table: "DonacionEspecie",
                column: "NecesidadId");

            migrationBuilder.CreateIndex(
                name: "IX_NecesidadEspecie_EsalId_Activa",
                table: "NecesidadEspecie",
                columns: new[] { "EsalId", "Activa" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonacionEspecie");

            migrationBuilder.DropTable(
                name: "NecesidadEspecie");

            migrationBuilder.DropColumn(
                name: "Organizacion",
                table: "AspNetUsers");
        }
    }
}
