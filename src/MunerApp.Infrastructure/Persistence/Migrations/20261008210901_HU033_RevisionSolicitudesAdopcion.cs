using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU033_RevisionSolicitudesAdopcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRevision",
                table: "SolicitudAdopcion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "SolicitudAdopcion",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisadoPorId",
                table: "SolicitudAdopcion",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaRevision",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "SolicitudAdopcion");

            migrationBuilder.DropColumn(
                name: "RevisadoPorId",
                table: "SolicitudAdopcion");
        }
    }
}
