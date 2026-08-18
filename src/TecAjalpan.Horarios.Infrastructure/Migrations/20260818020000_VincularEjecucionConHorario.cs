using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818020000_VincularEjecucionConHorario")]
public partial class VincularEjecucionConHorario : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "HorarioVersionId",
            schema: "Horarios",
            table: "EjecucionesGenerador",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EjecucionesGenerador_HorarioVersionId",
            schema: "Horarios",
            table: "EjecucionesGenerador",
            column: "HorarioVersionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EjecucionesGenerador_HorarioVersionId",
            schema: "Horarios",
            table: "EjecucionesGenerador");

        migrationBuilder.DropColumn(
            name: "HorarioVersionId",
            schema: "Horarios",
            table: "EjecucionesGenerador");
    }
}
