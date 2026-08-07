using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260807180000_AgregarEspaciosCarrerasCompartidas")]
public partial class AgregarEspaciosCarrerasCompartidas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EspaciosCarrerasCompartidas",
            schema: "Catalogos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_EspaciosCarrerasCompartidas", x => x.Id);
                table.ForeignKey(
                    name: "FK_EspaciosCarrerasCompartidas_Carreras_CarreraId",
                    column: x => x.CarreraId,
                    principalSchema: "Catalogos",
                    principalTable: "Carreras",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EspaciosCarrerasCompartidas_Espacios_EspacioId",
                    column: x => x.EspacioId,
                    principalSchema: "Catalogos",
                    principalTable: "Espacios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EspaciosCarrerasCompartidas_CarreraId",
            schema: "Catalogos",
            table: "EspaciosCarrerasCompartidas",
            column: "CarreraId");

        migrationBuilder.CreateIndex(
            name: "IX_EspaciosCarrerasCompartidas_EspacioId_CarreraId",
            schema: "Catalogos",
            table: "EspaciosCarrerasCompartidas",
            columns: new[] { "EspacioId", "CarreraId" },
            unique: true,
            filter: "[Eliminado] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EspaciosCarrerasCompartidas",
            schema: "Catalogos");
    }
}
