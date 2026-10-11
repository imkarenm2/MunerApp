using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU022_NovedadesApadrinados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NovedadBeneficiario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublicadaPorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NovedadBeneficiario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NovedadBeneficiario_Beneficiario_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FotoNovedad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    NovedadId = table.Column<int>(type: "int", nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotoNovedad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotoNovedad_NovedadBeneficiario_NovedadId",
                        column: x => x.NovedadId,
                        principalTable: "NovedadBeneficiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotoNovedad_NovedadId",
                table: "FotoNovedad",
                column: "NovedadId");

            migrationBuilder.CreateIndex(
                name: "IX_NovedadBeneficiario_BeneficiarioId_Fecha",
                table: "NovedadBeneficiario",
                columns: new[] { "BeneficiarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotoNovedad");

            migrationBuilder.DropTable(
                name: "NovedadBeneficiario");
        }
    }
}
