using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_Apadrinamientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApadrinamientoId",
                table: "Donacion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Apadrinamiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: false),
                    PadrinoId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ValorMensual = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCancelacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apadrinamiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apadrinamiento_AspNetUsers_PadrinoId",
                        column: x => x.PadrinoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Apadrinamiento_Beneficiario_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Apadrinamiento_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_ApadrinamientoId",
                table: "Donacion",
                column: "ApadrinamientoId");

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinamiento_BeneficiarioId",
                table: "Apadrinamiento",
                column: "BeneficiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinamiento_EsalId_BeneficiarioId_Estado",
                table: "Apadrinamiento",
                columns: new[] { "EsalId", "BeneficiarioId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinamiento_PadrinoId",
                table: "Apadrinamiento",
                column: "PadrinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinamiento_PadrinoId_BeneficiarioId",
                table: "Apadrinamiento",
                columns: new[] { "PadrinoId", "BeneficiarioId" },
                unique: true,
                filter: "[Estado] = N'Activo'");

            migrationBuilder.AddForeignKey(
                name: "FK_Donacion_Apadrinamiento_ApadrinamientoId",
                table: "Donacion",
                column: "ApadrinamientoId",
                principalTable: "Apadrinamiento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Donacion_Apadrinamiento_ApadrinamientoId",
                table: "Donacion");

            migrationBuilder.DropTable(
                name: "Apadrinamiento");

            migrationBuilder.DropIndex(
                name: "IX_Donacion_ApadrinamientoId",
                table: "Donacion");

            migrationBuilder.DropColumn(
                name: "ApadrinamientoId",
                table: "Donacion");
        }
    }
}
