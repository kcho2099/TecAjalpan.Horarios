using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260730180000_AgregarEspacioBaseGrupo")]
public partial class AgregarEspacioBaseGrupo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "EspacioBaseId",
            schema: "Academico",
            table: "Grupos",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Grupos_EspacioBaseId",
            schema: "Academico",
            table: "Grupos",
            column: "EspacioBaseId");

        migrationBuilder.AddForeignKey(
            name: "FK_Grupos_Espacios_EspacioBaseId",
            schema: "Academico",
            table: "Grupos",
            column: "EspacioBaseId",
            principalSchema: "Catalogos",
            principalTable: "Espacios",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Grupos_Espacios_EspacioBaseId",
            schema: "Academico",
            table: "Grupos");

        migrationBuilder.DropIndex(
            name: "IX_Grupos_EspacioBaseId",
            schema: "Academico",
            table: "Grupos");

        migrationBuilder.DropColumn(
            name: "EspacioBaseId",
            schema: "Academico",
            table: "Grupos");
    }
}
