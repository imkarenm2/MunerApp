using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MunerApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HU036_AprobacionVoluntarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRetiro",
                table: "PostulacionVoluntario",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "PostulacionVoluntario",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisadoPorId",
                table: "PostulacionVoluntario",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaRetiro",
                table: "PostulacionVoluntario");

            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "PostulacionVoluntario");

            migrationBuilder.DropColumn(
                name: "RevisadoPorId",
                table: "PostulacionVoluntario");
        }
    }
}
