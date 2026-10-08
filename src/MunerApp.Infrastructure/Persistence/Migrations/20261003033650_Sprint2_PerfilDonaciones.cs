using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_PerfilDonaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ciudad",
                table: "Esal",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescripcionCorta",
                table: "Esal",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacionPerfil",
                table: "Esal",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Historia",
                table: "Esal",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoRuta",
                table: "Esal",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mision",
                table: "Esal",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Esal",
                type: "nvarchar(90)",
                maxLength: 90,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Esal",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vision",
                table: "Esal",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VoluntariadoPausado",
                table: "Esal",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DatosDonacion",
                columns: table => new
                {
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Titular = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DocumentoTitular = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoCuenta = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Instrucciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatosDonacion", x => x.EsalId);
                    table.ForeignKey(
                        name: "FK_DatosDonacion_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoTransparencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ruta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Visible = table.Column<bool>(type: "bit", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaOcultado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoTransparencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoTransparencia_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Donacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DonanteId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    FechaTransferencia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MedioPago = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenciaPago = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SoporteRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FechaReporte = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevisadoPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Donacion_AspNetUsers_DonanteId",
                        column: x => x.DonanteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Donacion_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FotoEsal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotoEsal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotoEsal_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Icono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacion_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostulacionVoluntario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Disponibilidad = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Motivacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Programa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Semestre = table.Column<int>(type: "int", nullable: true),
                    SoporteAcademicoRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaPostulacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostulacionVoluntario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostulacionVoluntario_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostulacionVoluntario_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Esal_Slug",
                table: "Esal",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoTransparencia_EsalId_Visible",
                table: "DocumentoTransparencia",
                columns: new[] { "EsalId", "Visible" });

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_Codigo",
                table: "Donacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_DonanteId",
                table: "Donacion",
                column: "DonanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_EsalId_Estado",
                table: "Donacion",
                columns: new[] { "EsalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_FotoEsal_EsalId",
                table: "FotoEsal",
                column: "EsalId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_UsuarioId_Leida",
                table: "Notificacion",
                columns: new[] { "UsuarioId", "Leida" });

            migrationBuilder.CreateIndex(
                name: "IX_PostulacionVoluntario_EsalId_UsuarioId_Estado",
                table: "PostulacionVoluntario",
                columns: new[] { "EsalId", "UsuarioId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_PostulacionVoluntario_UsuarioId",
                table: "PostulacionVoluntario",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatosDonacion");

            migrationBuilder.DropTable(
                name: "DocumentoTransparencia");

            migrationBuilder.DropTable(
                name: "Donacion");

            migrationBuilder.DropTable(
                name: "FotoEsal");

            migrationBuilder.DropTable(
                name: "Notificacion");

            migrationBuilder.DropTable(
                name: "PostulacionVoluntario");

            migrationBuilder.DropIndex(
                name: "IX_Esal_Slug",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "Ciudad",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "DescripcionCorta",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "FechaActualizacionPerfil",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "Historia",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "LogoRuta",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "Mision",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "Vision",
                table: "Esal");

            migrationBuilder.DropColumn(
                name: "VoluntariadoPausado",
                table: "Esal");
        }
    }
}
