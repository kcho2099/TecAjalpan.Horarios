using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260725050000_AgregarPerfilSabatinoJornadaDocente")]
public partial class AgregarPerfilSabatinoJornadaDocente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_JornadasDocentes_DisponibilidadDocenteId_Dia",
            schema: "Recursos",
            table: "JornadasDocentes");

        migrationBuilder.AddColumn<bool>(
            name: "EsSemanaSabatina",
            schema: "Recursos",
            table: "JornadasDocentes",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_JornadasDocentes_DisponibilidadDocenteId_Dia_EsSemanaSabatina",
            schema: "Recursos",
            table: "JornadasDocentes",
            columns: new[]
            {
                "DisponibilidadDocenteId",
                "Dia",
                "EsSemanaSabatina"
            },
            unique: true,
            filter: "[Eliminado] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_JornadasDocentes_DisponibilidadDocenteId_Dia_EsSemanaSabatina",
            schema: "Recursos",
            table: "JornadasDocentes");

        migrationBuilder.DropColumn(
            name: "EsSemanaSabatina",
            schema: "Recursos",
            table: "JornadasDocentes");

        migrationBuilder.CreateIndex(
            name: "IX_JornadasDocentes_DisponibilidadDocenteId_Dia",
            schema: "Recursos",
            table: "JornadasDocentes",
            columns: new[] { "DisponibilidadDocenteId", "Dia" },
            unique: true,
            filter: "[Eliminado] = 0");
    }
}
