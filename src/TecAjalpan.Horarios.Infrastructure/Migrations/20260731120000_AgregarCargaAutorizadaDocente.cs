using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731120000_AgregarCargaAutorizadaDocente")]
public partial class AgregarCargaAutorizadaDocente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutorizacionesCargaDocentes",
            schema: "Recursos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HorasAutorizadas = table.Column<byte>(type: "tinyint", nullable: false),
                UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                Eliminado = table.Column<bool>(type: "bit", nullable: false),
                UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutorizacionesCargaDocentes", x => x.Id);
                table.ForeignKey(
                    name: "FK_AutorizacionesCargaDocentes_Docentes_DocenteId",
                    column: x => x.DocenteId,
                    principalSchema: "Recursos",
                    principalTable: "Docentes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AutorizacionesCargaDocentes_Periodos_PeriodoId",
                    column: x => x.PeriodoId,
                    principalSchema: "Academico",
                    principalTable: "Periodos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AutorizacionesCargaDocentes_DocenteId",
            schema: "Recursos",
            table: "AutorizacionesCargaDocentes",
            column: "DocenteId");

        migrationBuilder.CreateIndex(
            name: "IX_AutorizacionesCargaDocentes_PeriodoId_DocenteId",
            schema: "Recursos",
            table: "AutorizacionesCargaDocentes",
            columns: new[] { "PeriodoId", "DocenteId" },
            unique: true,
            filter: "[Eliminado] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AutorizacionesCargaDocentes",
            schema: "Recursos");
    }
}
