using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

public partial class AgregarDocentesCarreras : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DocentesCarreras",
            schema: "Recursos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EsPrincipal = table.Column<bool>(type: "bit", nullable: false),
                UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                Eliminado = table.Column<bool>(type: "bit", nullable: false),
                UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(
                    type: "rowversion",
                    rowVersion: true,
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocentesCarreras", x => x.Id);
                table.ForeignKey(
                    name: "FK_DocentesCarreras_Carreras_CarreraId",
                    column: x => x.CarreraId,
                    principalSchema: "Catalogos",
                    principalTable: "Carreras",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DocentesCarreras_Docentes_DocenteId",
                    column: x => x.DocenteId,
                    principalSchema: "Recursos",
                    principalTable: "Docentes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DocentesCarreras_CarreraId",
            schema: "Recursos",
            table: "DocentesCarreras",
            column: "CarreraId");

        migrationBuilder.CreateIndex(
            name: "IX_DocentesCarreras_DocenteId_CarreraId",
            schema: "Recursos",
            table: "DocentesCarreras",
            columns: new[] { "DocenteId", "CarreraId" },
            unique: true,
            filter: "[Eliminado] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_DocentesCarreras_DocenteId",
            schema: "Recursos",
            table: "DocentesCarreras",
            column: "DocenteId",
            unique: true,
            filter: "[EsPrincipal] = 1 AND [Eliminado] = 0");

        migrationBuilder.CreateTable(
            name: "JornadasDocentes",
            schema: "Recursos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisponibilidadDocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Dia = table.Column<byte>(type: "tinyint", nullable: false),
                HoraInicio = table.Column<TimeOnly>(type: "time", nullable: false),
                HoraFin = table.Column<TimeOnly>(type: "time", nullable: false),
                UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                Eliminado = table.Column<bool>(type: "bit", nullable: false),
                UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(
                    type: "rowversion",
                    rowVersion: true,
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JornadasDocentes", x => x.Id);
                table.ForeignKey(
                    name: "FK_JornadasDocentes_DisponibilidadesDocentes_DisponibilidadDocenteId",
                    column: x => x.DisponibilidadDocenteId,
                    principalSchema: "Recursos",
                    principalTable: "DisponibilidadesDocentes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_JornadasDocentes_DisponibilidadDocenteId_Dia",
            schema: "Recursos",
            table: "JornadasDocentes",
            columns: new[] { "DisponibilidadDocenteId", "Dia" },
            unique: true,
            filter: "[Eliminado] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "JornadasDocentes",
            schema: "Recursos");

        migrationBuilder.DropTable(
            name: "DocentesCarreras",
            schema: "Recursos");
    }
}
