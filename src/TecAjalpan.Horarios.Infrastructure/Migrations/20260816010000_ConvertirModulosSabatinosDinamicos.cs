using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260816010000_ConvertirModulosSabatinosDinamicos")]
public partial class ConvertirModulosSabatinosDinamicos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE #ModulosNormalizados
            (
                ModuloAnteriorId UNIQUEIDENTIFIER NOT NULL,
                ModuloNuevoId UNIQUEIDENTIFIER NOT NULL,
                ModuloMateriaId UNIQUEIDENTIFIER NOT NULL,
                ConfiguracionSabatinaId UNIQUEIDENTIFIER NOT NULL,
                OrdenTemporal TINYINT NOT NULL,
                OrdenFinal TINYINT NOT NULL,
                Semanas TINYINT NOT NULL,
                FechaInicio DATE NOT NULL,
                FechaFin DATE NOT NULL,
                UsuarioCrea NVARCHAR(MAX) NOT NULL,
                FechaCrea DATETIME2 NOT NULL,
                UsuarioModifica NVARCHAR(MAX) NULL,
                FechaModifica DATETIME2 NULL,
                Eliminado BIT NOT NULL,
                UsuarioElimina NVARCHAR(MAX) NULL,
                FechaElimina DATETIME2 NULL
            );

            WITH MateriasOrdenadas AS
            (
                SELECT
                    ms.Id AS ModuloAnteriorId,
                    mm.Id AS ModuloMateriaId,
                    ms.ConfiguracionSabatinaId,
                    mm.Turno,
                    cs.FechaInicio AS FechaInicioConfiguracion,
                    ms.UsuarioCrea,
                    ms.FechaCrea,
                    ms.UsuarioModifica,
                    ms.FechaModifica,
                    ms.Eliminado,
                    ms.UsuarioElimina,
                    ms.FechaElimina,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY ms.ConfiguracionSabatinaId
                        ORDER BY mm.Turno, ms.Orden, mm.Id
                    ) AS OrdenGlobal,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY ms.ConfiguracionSabatinaId, mm.Turno
                        ORDER BY ms.Orden, mm.Id
                    ) AS PosicionTurno,
                    COUNT(*) OVER
                    (
                        PARTITION BY ms.ConfiguracionSabatinaId, mm.Turno
                    ) AS MateriasTurno
                FROM Sabatino.ModulosSabatinos ms
                INNER JOIN Sabatino.ModulosMaterias mm
                    ON mm.ModuloSabatinoId = ms.Id
                INNER JOIN Sabatino.ConfiguracionesSabatinas cs
                    ON cs.Id = ms.ConfiguracionSabatinaId
                WHERE ms.Eliminado = 0
                    AND mm.Eliminado = 0
            ),
            Distribucion AS
            (
                SELECT *,
                    18 / MateriasTurno
                        + CASE WHEN PosicionTurno <= 18 % MateriasTurno THEN 1 ELSE 0 END
                        AS Duracion,
                    (PosicionTurno - 1) * (18 / MateriasTurno)
                        + CASE
                            WHEN PosicionTurno - 1 < 18 % MateriasTurno
                                THEN PosicionTurno - 1
                            ELSE 18 % MateriasTurno
                          END AS Desplazamiento
                FROM MateriasOrdenadas
            )
            INSERT INTO #ModulosNormalizados
            SELECT
                ModuloAnteriorId,
                NEWID(),
                ModuloMateriaId,
                ConfiguracionSabatinaId,
                CAST(OrdenGlobal + 100 AS TINYINT),
                CAST(OrdenGlobal AS TINYINT),
                CAST(Duracion AS TINYINT),
                DATEADD(DAY, Desplazamiento * 7, FechaInicioConfiguracion),
                DATEADD(DAY, (Desplazamiento + Duracion - 1) * 7, FechaInicioConfiguracion),
                UsuarioCrea,
                FechaCrea,
                UsuarioModifica,
                FechaModifica,
                Eliminado,
                UsuarioElimina,
                FechaElimina
            FROM Distribucion;

            INSERT INTO Sabatino.ModulosSabatinos
            (
                Id, ConfiguracionSabatinaId, Orden, Semanas, FechaInicio, FechaFin,
                UsuarioCrea, FechaCrea, UsuarioModifica, FechaModifica,
                Eliminado, UsuarioElimina, FechaElimina
            )
            SELECT
                ModuloNuevoId, ConfiguracionSabatinaId, OrdenTemporal, Semanas,
                FechaInicio, FechaFin, UsuarioCrea, FechaCrea, UsuarioModifica,
                FechaModifica, Eliminado, UsuarioElimina, FechaElimina
            FROM #ModulosNormalizados;

            UPDATE mm
            SET mm.ModuloSabatinoId = normalizado.ModuloNuevoId
            FROM Sabatino.ModulosMaterias mm
            INNER JOIN #ModulosNormalizados normalizado
                ON normalizado.ModuloMateriaId = mm.Id;

            DELETE ms
            FROM Sabatino.ModulosSabatinos ms
            WHERE EXISTS
            (
                SELECT 1
                FROM #ModulosNormalizados normalizado
                WHERE normalizado.ConfiguracionSabatinaId = ms.ConfiguracionSabatinaId
            )
            AND NOT EXISTS
            (
                SELECT 1
                FROM #ModulosNormalizados normalizado
                WHERE normalizado.ModuloNuevoId = ms.Id
            );

            UPDATE ms
            SET ms.Orden = normalizado.OrdenFinal
            FROM Sabatino.ModulosSabatinos ms
            INNER JOIN #ModulosNormalizados normalizado
                ON normalizado.ModuloNuevoId = ms.Id;

            UPDATE cs
            SET cs.Validada = 1
            FROM Sabatino.ConfiguracionesSabatinas cs
            WHERE EXISTS
            (
                SELECT 1
                FROM #ModulosNormalizados normalizado
                WHERE normalizado.ConfiguracionSabatinaId = cs.Id
            );

            DROP TABLE #ModulosNormalizados;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "La conversión de módulos sabatinos dinámicos modifica la distribución académica y no puede revertirse automáticamente.");
    }
}
