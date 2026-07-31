using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731173000_AgregarRolesCargaAcademica")]
public partial class AgregarRolesCargaAcademica : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CargasAcademicas_OfertaMateriaId",
            schema: "Operacion",
            table: "CargasAcademicas");

        migrationBuilder.AddColumn<byte>(
            name: "HorasAsignadas",
            schema: "Operacion",
            table: "CargasAcademicas",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<byte>(
            name: "Rol",
            schema: "Operacion",
            table: "CargasAcademicas",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)1);

        migrationBuilder.Sql(
            """
            UPDATE carga
            SET carga.HorasAsignadas = oferta.HorasRequeridas
            FROM [Operacion].[CargasAcademicas] AS carga
            INNER JOIN [Academico].[OfertasMaterias] AS oferta
                ON oferta.Id = carga.OfertaMateriaId;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_CargasAcademicas_OfertaMateriaId_Rol",
            schema: "Operacion",
            table: "CargasAcademicas",
            columns: new[] { "OfertaMateriaId", "Rol" },
            unique: true,
            filter: "[Eliminado] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CargasAcademicas_OfertaMateriaId_Rol",
            schema: "Operacion",
            table: "CargasAcademicas");

        migrationBuilder.Sql(
            "DELETE FROM [Operacion].[CargasAcademicas] WHERE Rol = 2;");

        migrationBuilder.DropColumn(
            name: "HorasAsignadas",
            schema: "Operacion",
            table: "CargasAcademicas");

        migrationBuilder.DropColumn(
            name: "Rol",
            schema: "Operacion",
            table: "CargasAcademicas");

        migrationBuilder.CreateIndex(
            name: "IX_CargasAcademicas_OfertaMateriaId",
            schema: "Operacion",
            table: "CargasAcademicas",
            column: "OfertaMateriaId",
            unique: true,
            filter: "[Eliminado] = 0");
    }
}
