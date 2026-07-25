using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260725043000_RestringirPeriodoActivoUnico")]
public partial class RestringirPeriodoActivoUnico : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [Academico].[Periodos]
            SET [Estado] = 3
            WHERE [Estado] = 2
              AND [Eliminado] = 0
              AND [FechaFin] < CONVERT(date, GETDATE());

            ;WITH [PeriodosActivos] AS
            (
                SELECT
                    [Id],
                    ROW_NUMBER() OVER (
                        ORDER BY [FechaInicio] DESC, [FechaCrea] DESC, [Id]
                    ) AS [Numero]
                FROM [Academico].[Periodos]
                WHERE [Estado] = 2
                  AND [Eliminado] = 0
            )
            UPDATE [Periodo]
            SET [Estado] = 3
            FROM [Academico].[Periodos] AS [Periodo]
            INNER JOIN [PeriodosActivos] AS [Activo]
                ON [Activo].[Id] = [Periodo].[Id]
            WHERE [Activo].[Numero] > 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Periodos_UnicoActivo'
                  AND [object_id] = OBJECT_ID(N'[Academico].[Periodos]')
            )
            BEGIN
                CREATE UNIQUE INDEX [UX_Periodos_UnicoActivo]
                    ON [Academico].[Periodos] ([Estado])
                    WHERE [Estado] = 2 AND [Eliminado] = 0;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'UX_Periodos_UnicoActivo'
                  AND [object_id] = OBJECT_ID(N'[Academico].[Periodos]')
            )
            BEGIN
                DROP INDEX [UX_Periodos_UnicoActivo]
                    ON [Academico].[Periodos];
            END;
            """);
    }
}
