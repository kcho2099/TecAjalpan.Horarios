/*
    Quita lógicamente las asignaciones sabatinas de:
        Diego Maldonado, Raúl Alberto

    No elimina materias, ofertas, grupos, módulos, configuraciones ni
    disponibilidad docente. Únicamente modifica Operacion.CargasAcademicas.

    EstadoCarga: 1 = Borrador, 2 = Autorizada, 3 = Devuelta.
    TipoModalidad: 1 = Escolarizada, 2 = Sabatina.

    INSTRUCCIONES
    1. Consulte el PeriodoId con la primera consulta.
    2. Colóquelo en @PeriodoId.
    3. Ejecute con @Confirmar = 0 para revisar.
    4. Después de validar el resultado, cambie @Confirmar = 1.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Consulte aquí el identificador del periodo que desea limpiar. */
SELECT
    p.Id AS PeriodoId,
    p.Nombre,
    p.FechaInicio,
    p.FechaFin,
    p.Estado
FROM Academico.Periodos AS p
WHERE p.Eliminado = 0
ORDER BY p.FechaInicio DESC;

DECLARE @PeriodoId UNIQUEIDENTIFIER = NULL;  -- OBLIGATORIO
DECLARE @Confirmar BIT = 0;                  -- 0 = simulación, 1 = ejecutar
DECLARE @PermitirCargasAutorizadas BIT = 0;  -- Cambiar a 1 sólo si ya se revisaron
DECLARE @Usuario NVARCHAR(450) = COALESCE(SUSER_SNAME(), N'script-sql');

IF @PeriodoId IS NULL
    THROW 51000, N'Debe colocar el PeriodoId antes de ejecutar el script.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM Academico.Periodos AS p
    WHERE p.Id = @PeriodoId
      AND p.Eliminado = 0
)
    THROW 51001, N'El PeriodoId indicado no existe o está eliminado.', 1;

DECLARE @DocenteId UNIQUEIDENTIFIER;
DECLARE @CoincidenciasDocente INT;

SELECT
    @CoincidenciasDocente = COUNT(*)
FROM Recursos.Docentes AS d
WHERE d.Eliminado = 0
  AND d.Apellidos = N'Diego Maldonado'
  AND d.Nombres = N'Raúl Alberto';

IF @CoincidenciasDocente = 0
    THROW 51002, N'No se encontró al docente Diego Maldonado, Raúl Alberto.', 1;

IF @CoincidenciasDocente > 1
    THROW 51003, N'Existe más de un docente con ese nombre. Use NumeroTrabajador para identificarlo antes de continuar.', 1;

SELECT @DocenteId = d.Id
FROM Recursos.Docentes AS d
WHERE d.Eliminado = 0
  AND d.Apellidos = N'Diego Maldonado'
  AND d.Nombres = N'Raúl Alberto';

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE #CargasObjetivo
    (
        CargaAcademicaId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        EstadoCarga TINYINT NOT NULL,
        Periodo NVARCHAR(120) NOT NULL,
        Carrera NVARCHAR(200) NOT NULL,
        GrupoClave NVARCHAR(30) NOT NULL,
        MateriaClave NVARCHAR(30) NOT NULL,
        Materia NVARCHAR(200) NOT NULL,
        Modulo TINYINT NULL,
        FechaInicio DATE NULL,
        FechaFin DATE NULL,
        Turno NVARCHAR(20) NULL
    );

    INSERT INTO #CargasObjetivo
    (
        CargaAcademicaId,
        EstadoCarga,
        Periodo,
        Carrera,
        GrupoClave,
        MateriaClave,
        Materia,
        Modulo,
        FechaInicio,
        FechaFin,
        Turno
    )
    SELECT
        ca.Id,
        ca.Estado,
        p.Nombre,
        c.Nombre,
        g.Clave,
        m.Clave,
        m.Nombre,
        modulo.Orden,
        modulo.FechaInicio,
        modulo.FechaFin,
        CASE modulo.Turno
            WHEN 1 THEN N'Matutino'
            WHEN 2 THEN N'Vespertino'
        END
    FROM Operacion.CargasAcademicas AS ca WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN Academico.OfertasMaterias AS om
        ON om.Id = ca.OfertaMateriaId
    INNER JOIN Academico.Grupos AS g
        ON g.Id = om.GrupoId
    INNER JOIN Academico.PeriodosCarreras AS pc
        ON pc.Id = g.PeriodoCarreraId
    INNER JOIN Academico.Periodos AS p
        ON p.Id = pc.PeriodoId
    INNER JOIN Catalogos.Carreras AS c
        ON c.Id = pc.CarreraId
    INNER JOIN Catalogos.Modalidades AS md
        ON md.Id = pc.ModalidadId
    INNER JOIN Catalogos.Materias AS m
        ON m.Id = om.MateriaId
    OUTER APPLY
    (
        SELECT TOP (1)
            ms.Orden,
            ms.FechaInicio,
            ms.FechaFin,
            mm.Turno
        FROM Sabatino.ModulosMaterias AS mm
        INNER JOIN Sabatino.ModulosSabatinos AS ms
            ON ms.Id = mm.ModuloSabatinoId
        INNER JOIN Sabatino.ConfiguracionesSabatinas AS cs
            ON cs.Id = ms.ConfiguracionSabatinaId
        WHERE mm.OfertaMateriaId = om.Id
          AND mm.Eliminado = 0
          AND ms.Eliminado = 0
          AND cs.Eliminado = 0
        ORDER BY ms.FechaInicio, ms.Orden
    ) AS modulo
    WHERE ca.DocenteId = @DocenteId
      AND ca.Eliminado = 0
      AND pc.PeriodoId = @PeriodoId
      AND md.Tipo = 2;

    /* Siempre se muestra exactamente lo que sería afectado. */
    SELECT
        CargaAcademicaId,
        CASE EstadoCarga
            WHEN 1 THEN N'Borrador'
            WHEN 2 THEN N'Autorizada'
            WHEN 3 THEN N'Devuelta'
            ELSE CONCAT(N'Desconocido (', EstadoCarga, N')')
        END AS EstadoCarga,
        Periodo,
        Carrera,
        GrupoClave,
        MateriaClave,
        Materia,
        Modulo,
        FechaInicio,
        FechaFin,
        Turno
    FROM #CargasObjetivo
    ORDER BY FechaInicio, Turno, MateriaClave;

    IF NOT EXISTS (SELECT 1 FROM #CargasObjetivo)
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'No existen cargas sabatinas vigentes asignadas a Diego Maldonado en el periodo indicado.';
        RETURN;
    END;

    IF @Confirmar = 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'SIMULACIÓN: no se modificó ningún registro. Revise el resultado y cambie @Confirmar a 1 para ejecutar.';
        RETURN;
    END;

    IF @PermitirCargasAutorizadas = 0
       AND EXISTS
       (
           SELECT 1
           FROM #CargasObjetivo
           WHERE EstadoCarga = 2
       )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51004, N'Hay cargas autorizadas. Revise el resultado y cambie @PermitirCargasAutorizadas a 1 si realmente deben quitarse.', 1;
    END;

    /* No se tocan sesiones ni versiones de horario. Se evita dejar una versión
       generada apuntando a una carga eliminada sin una decisión explícita. */
    IF EXISTS
    (
        SELECT 1
        FROM Horarios.SesionesHorario AS sh
        INNER JOIN #CargasObjetivo AS objetivo
            ON objetivo.CargaAcademicaId = sh.CargaAcademicaId
        WHERE sh.Eliminado = 0
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51005, N'Una o más cargas ya tienen sesiones de horario. No se modificó nada; primero debe revisarse la versión de horario relacionada.', 1;
    END;

    DECLARE @Fecha DATETIME2 = SYSUTCDATETIME();

    UPDATE ca
    SET
        ca.Estado = 3,
        ca.Eliminado = 1,
        ca.FechaAutorizacion = NULL,
        ca.UsuarioAutoriza = NULL,
        ca.UsuarioModifica = @Usuario,
        ca.FechaModifica = @Fecha,
        ca.UsuarioElimina = @Usuario,
        ca.FechaElimina = @Fecha
    FROM Operacion.CargasAcademicas AS ca
    INNER JOIN #CargasObjetivo AS objetivo
        ON objetivo.CargaAcademicaId = ca.Id
    WHERE ca.Eliminado = 0;

    DECLARE @RegistrosAfectados INT = @@ROWCOUNT;

    COMMIT TRANSACTION;

    SELECT
        @RegistrosAfectados AS CargasSabatinasQuitadas,
        @DocenteId AS DocenteId,
        @PeriodoId AS PeriodoId,
        @Usuario AS UsuarioEjecucion,
        @Fecha AS FechaUTC;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
