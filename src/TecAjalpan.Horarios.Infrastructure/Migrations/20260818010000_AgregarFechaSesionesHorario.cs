using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818010000_AgregarFechaSesionesHorario")]
public partial class AgregarFechaSesionesHorario : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_DocenteId",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.DropIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_EspacioId",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.DropIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_GrupoId",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.AddColumn<DateOnly>(
            name: "Fecha",
            schema: "Horarios",
            table: "SesionesHorario",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1900, 1, 1));

        migrationBuilder.CreateIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Fecha_Bloque_DocenteId",
            schema: "Horarios",
            table: "SesionesHorario",
            columns: new[] { "HorarioVersionId", "Fecha", "Bloque", "DocenteId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Fecha_Bloque_EspacioId",
            schema: "Horarios",
            table: "SesionesHorario",
            columns: new[] { "HorarioVersionId", "Fecha", "Bloque", "EspacioId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Fecha_Bloque_GrupoId",
            schema: "Horarios",
            table: "SesionesHorario",
            columns: new[] { "HorarioVersionId", "Fecha", "Bloque", "GrupoId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Fecha_Bloque_DocenteId",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.DropIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Fecha_Bloque_EspacioId",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.DropIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Fecha_Bloque_GrupoId",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.DropColumn(
            name: "Fecha",
            schema: "Horarios",
            table: "SesionesHorario");

        migrationBuilder.CreateIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_DocenteId",
            schema: "Horarios",
            table: "SesionesHorario",
            columns: new[] { "HorarioVersionId", "Dia", "Bloque", "DocenteId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_EspacioId",
            schema: "Horarios",
            table: "SesionesHorario",
            columns: new[] { "HorarioVersionId", "Dia", "Bloque", "EspacioId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_GrupoId",
            schema: "Horarios",
            table: "SesionesHorario",
            columns: new[] { "HorarioVersionId", "Dia", "Bloque", "GrupoId" },
            unique: true);
    }
}
