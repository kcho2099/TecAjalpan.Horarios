using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729160000_AgregarPlanesEstudioMaterias")]
public partial class AgregarPlanesEstudioMaterias : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "HorasPracticas", schema: "Catalogos", table: "Materias",
            type: "tinyint", nullable: false, defaultValue: (byte)0);
        migrationBuilder.AddColumn<byte>(
            name: "HorasTeoricas", schema: "Catalogos", table: "Materias",
            type: "tinyint", nullable: false, defaultValue: (byte)0);

        migrationBuilder.Sql(
            "UPDATE [Catalogos].[Materias] SET [HorasTeoricas] = [HorasSemanales] WHERE [HorasTeoricas] = 0 AND [HorasPracticas] = 0;");

        migrationBuilder.DropIndex(
            name: "IX_Reticulas_Clave", schema: "Catalogos", table: "Reticulas");
        migrationBuilder.DropIndex(
            name: "IX_Materias_Clave", schema: "Catalogos", table: "Materias");

        migrationBuilder.CreateTable(
            name: "MateriasModalidades",
            schema: "Catalogos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MateriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModalidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_MateriasModalidades", x => x.Id);
                table.ForeignKey("FK_MateriasModalidades_Materias_MateriaId", x => x.MateriaId,
                    principalSchema: "Catalogos", principalTable: "Materias", principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_MateriasModalidades_Modalidades_ModalidadId", x => x.ModalidadId,
                    principalSchema: "Catalogos", principalTable: "Modalidades", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Reticulas_CarreraId_Clave", schema: "Catalogos", table: "Reticulas",
            columns: new[] { "CarreraId", "Clave" }, unique: true,
            filter: "[Eliminado] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_Materias_ReticulaId_Clave", schema: "Catalogos", table: "Materias",
            columns: new[] { "ReticulaId", "Clave" }, unique: true,
            filter: "[Eliminado] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_MateriasModalidades_MateriaId_ModalidadId",
            schema: "Catalogos", table: "MateriasModalidades",
            columns: new[] { "MateriaId", "ModalidadId" }, unique: true,
            filter: "[Eliminado] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_MateriasModalidades_ModalidadId",
            schema: "Catalogos", table: "MateriasModalidades", column: "ModalidadId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MateriasModalidades", schema: "Catalogos");
        migrationBuilder.DropIndex(name: "IX_Reticulas_CarreraId_Clave", schema: "Catalogos", table: "Reticulas");
        migrationBuilder.DropIndex(name: "IX_Materias_ReticulaId_Clave", schema: "Catalogos", table: "Materias");
        migrationBuilder.DropColumn(name: "HorasPracticas", schema: "Catalogos", table: "Materias");
        migrationBuilder.DropColumn(name: "HorasTeoricas", schema: "Catalogos", table: "Materias");
        migrationBuilder.CreateIndex(name: "IX_Reticulas_Clave", schema: "Catalogos", table: "Reticulas",
            column: "Clave", unique: true, filter: "[Eliminado] = 0");
        migrationBuilder.CreateIndex(name: "IX_Materias_Clave", schema: "Catalogos", table: "Materias",
            column: "Clave", unique: true, filter: "[Eliminado] = 0");
    }
}
