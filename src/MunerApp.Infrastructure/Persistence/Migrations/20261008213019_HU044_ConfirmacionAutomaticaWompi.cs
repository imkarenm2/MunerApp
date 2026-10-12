using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU044_ConfirmacionAutomaticaWompi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "Donacion",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaPasarela",
                table: "Donacion",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransaccionPasarelaId",
                table: "Donacion",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventoPasarela",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsalId = table.Column<int>(type: "int", nullable: false),
                    Evento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TransaccionId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EstadoTransaccion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DonacionId = table.Column<int>(type: "int", nullable: true),
                    Cuerpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaRecepcion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoPasarela", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoPasarela_Donacion_DonacionId",
                        column: x => x.DonacionId,
                        principalTable: "Donacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoPasarela_Esal_EsalId",
                        column: x => x.EsalId,
                        principalTable: "Esal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_ReferenciaPasarela",
                table: "Donacion",
                column: "ReferenciaPasarela",
                unique: true,
                filter: "[ReferenciaPasarela] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Donacion_TransaccionPasarelaId",
                table: "Donacion",
                column: "TransaccionPasarelaId",
                unique: true,
                filter: "[TransaccionPasarelaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventoPasarela_DonacionId",
                table: "EventoPasarela",
                column: "DonacionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoPasarela_EsalId_FechaRecepcion",
                table: "EventoPasarela",
                columns: new[] { "EsalId", "FechaRecepcion" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoPasarela_TransaccionId",
                table: "EventoPasarela",
                column: "TransaccionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventoPasarela");

            migrationBuilder.DropIndex(
                name: "IX_Donacion_ReferenciaPasarela",
                table: "Donacion");

            migrationBuilder.DropIndex(
                name: "IX_Donacion_TransaccionPasarelaId",
                table: "Donacion");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "Donacion");

            migrationBuilder.DropColumn(
                name: "ReferenciaPasarela",
                table: "Donacion");

            migrationBuilder.DropColumn(
                name: "TransaccionPasarelaId",
                table: "Donacion");
        }
    }
}
