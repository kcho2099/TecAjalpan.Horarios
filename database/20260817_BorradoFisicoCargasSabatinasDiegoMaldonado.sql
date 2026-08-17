/*
    BORRADO FÍSICO SELECTIVO
    Cargas sabatinas en estado Borrador de Diego Maldonado, Raúl Alberto.

    Este script elimina filas de Operacion.CargasAcademicas de forma permanente.
    No modifica ni elimina materias, ofertas, grupos, módulos, configuraciones,
    disponibilidades o cargas escolarizadas.

    El borrado se selecciona por CargaAcademicaId para evitar eliminar una carga
    Borrador válida por accidente.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Consulte el PeriodoId y colóquelo en la variable siguiente. */
SELECT
    p.Id AS PeriodoId,
    p.Nombre,
    p.FechaInicio,
    p.FechaFin,
    p.Estado
FROM Academico.Periodos AS p
WHERE p.Eliminado = 0
ORDER BY p.FechaInicio DESC;

DECLARE @PeriodoId UNIQUEIDENTIFIER = NULL; -- OBLIGATORIO
DECLARE @Confirmar BIT = 0;                 -- 0 = simulación, 1 = borrar

/* Después de ejecutar una simulación, copie aquí solamente los
   CargaAcademicaId que realmente desea borrar físicamente. */
DECLARE @CargasSeleccionadas TABLE
(
    CargaAcademicaId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY
);

/* EJEMPLO; reemplace los identificadores y quite el comentario:
INSERT INTO @CargasSeleccionadas (CargaAcademicaId)
VALUES
    ('00000000-0000-0000-0000-000000000001'),
    ('00000000-0000-0000-0000-000000000002');
*/

IF @PeriodoId IS NULL
    THROW 51100, N'Debe colocar el PeriodoId antes de ejecutar el script.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM Academico.Periodos AS p
    WHERE p.Id = @PeriodoId
      AND p.Eliminado = 0
)
    THROW 51101, N'El PeriodoId indicado no existe o está eliminado.', 1;

DECLARE @DocenteId UNIQUEIDENTIFIER;
DECLARE @CoincidenciasDocente INT;

SELECT @CoincidenciasDocente = COUNT(*)
FROM Recursos.Docentes AS d
WHERE d.Eliminado = 0
  AND d.Apellidos = N'Diego Maldonado'
  AND d.Nombres = N'Raúl Alberto';

IF @CoincidenciasDocente = 0
    THROW 51102, N'No se encontró al docente Diego Maldonado, Raúl Alberto.', 1;

IF @CoincidenciasDocente > 1
    THROW 51103, N'Existe más de un docente con ese nombre. Identifíquelo por NumeroTrabajador antes de continuar.', 1;

SELECT @DocenteId = d.Id
FROM Recursos.Docentes AS d
WHERE d.Eliminado = 0
  AND d.Apellidos = N'Diego Maldonado'
  AND d.Nombres = N'Raúl Alberto';

CREATE TABLE #Candidatas
(
    CargaAcademicaId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CargaEliminada BIT NOT NULL,
    FechaElimina DATETIME2 NULL,
    UsuarioElimina NVARCHAR(MAX) NULL,
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

INSERT INTO #Candidatas
(
    CargaAcademicaId,
    CargaEliminada,
    FechaElimina,
    UsuarioElimina,
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
    ca.Eliminado,
    ca.FechaElimina,
    ca.UsuarioElimina,
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
FROM Operacion.CargasAcademicas AS ca
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
    ORDER BY
        mm.Eliminado,
        ms.Eliminado,
        cs.Eliminado,
        ms.FechaInicio,
        ms.Orden
) AS modulo
WHERE ca.DocenteId = @DocenteId
  AND ca.Estado = 1
  AND pc.PeriodoId = @PeriodoId
  AND md.Tipo = 2;

/* Resultado de diagnóstico: éstas son todas las cargas Borrador candidatas. */
SELECT
    CargaAcademicaId,
    CargaEliminada,
    FechaElimina,
    UsuarioElimina,
    Periodo,
    Carrera,
    GrupoClave,
    MateriaClave,
    Materia,
    Modulo,
    FechaInicio,
    FechaFin,
    Turno
FROM #Candidatas
ORDER BY FechaInicio, Turno, MateriaClave;

IF NOT EXISTS (SELECT 1 FROM #Candidatas)
BEGIN
    PRINT N'No existen cargas sabatinas Borrador de Diego Maldonado en el periodo indicado.';
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM @CargasSeleccionadas)
BEGIN
    PRINT N'SIMULACIÓN: copie los CargaAcademicaId requeridos en @CargasSeleccionadas. No se borró nada.';
    RETURN;
END;

/* Impide usar identificadores de otro docente, periodo, modalidad o estado. */
IF EXISTS
(
    SELECT 1
    FROM @CargasSeleccionadas AS seleccionada
    LEFT JOIN #Candidatas AS candidata
        ON candidata.CargaAcademicaId = seleccionada.CargaAcademicaId
    WHERE candidata.CargaAcademicaId IS NULL
)
    THROW 51104, N'Una carga seleccionada no corresponde a Diego, al periodo, a modalidad sabatina o al estado Borrador.', 1;

/* Muestra únicamente la selección que se borraría. */
SELECT candidata.*
FROM #Candidatas AS candidata
INNER JOIN @CargasSeleccionadas AS seleccionada
    ON seleccionada.CargaAcademicaId = candidata.CargaAcademicaId
ORDER BY candidata.FechaInicio, candidata.Turno, candidata.MateriaClave;

IF @Confirmar = 0
BEGIN
    PRINT N'SIMULACIÓN: selección válida. Cambie @Confirmar a 1 para realizar el borrado físico.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Bloquea las filas seleccionadas y vuelve a comprobar sus condiciones. */
    IF
    (
        SELECT COUNT(*)
        FROM Operacion.CargasAcademicas AS ca WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN @CargasSeleccionadas AS seleccionada
            ON seleccionada.CargaAcademicaId = ca.Id
        INNER JOIN Academico.OfertasMaterias AS om
            ON om.Id = ca.OfertaMateriaId
        INNER JOIN Academico.Grupos AS g
            ON g.Id = om.GrupoId
        INNER JOIN Academico.PeriodosCarreras AS pc
            ON pc.Id = g.PeriodoCarreraId
        INNER JOIN Catalogos.Modalidades AS md
            ON md.Id = pc.ModalidadId
        WHERE ca.DocenteId = @DocenteId
          AND ca.Estado = 1
          AND pc.PeriodoId = @PeriodoId
          AND md.Tipo = 2
    ) <> (SELECT COUNT(*) FROM @CargasSeleccionadas)
        THROW 51105, N'Las cargas cambiaron desde la simulación. No se borró nada; vuelva a revisar.', 1;

    /* Las relaciones NoAction impedirían el DELETE; se aborta antes y se
       informa para no borrar silenciosamente sesiones o pendientes. */
    IF EXISTS
    (
        SELECT 1
        FROM Horarios.SesionesHorario AS sh
        INNER JOIN @CargasSeleccionadas AS seleccionada
            ON seleccionada.CargaAcademicaId = sh.CargaAcademicaId
    )
        THROW 51106, N'Una carga seleccionada tiene sesiones de horario relacionadas. No se realizó el borrado físico.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM Horarios.PendientesGeneracion AS pg
        INNER JOIN @CargasSeleccionadas AS seleccionada
            ON seleccionada.CargaAcademicaId = pg.CargaAcademicaId
    )
        THROW 51107, N'Una carga seleccionada tiene pendientes de generación relacionados. No se realizó el borrado físico.', 1;

    DECLARE @Eliminadas TABLE
    (
        CargaAcademicaId UNIQUEIDENTIFIER NOT NULL,
        OfertaMateriaId UNIQUEIDENTIFIER NOT NULL,
        DocenteId UNIQUEIDENTIFIER NOT NULL,
        EstadoAnterior TINYINT NOT NULL,
        EstabaEliminadaLogicamente BIT NOT NULL
    );

    DELETE ca
    OUTPUT
        deleted.Id,
        deleted.OfertaMateriaId,
        deleted.DocenteId,
        deleted.Estado,
        deleted.Eliminado
    INTO @Eliminadas
    (
        CargaAcademicaId,
        OfertaMateriaId,
        DocenteId,
        EstadoAnterior,
        EstabaEliminadaLogicamente
    )
    FROM Operacion.CargasAcademicas AS ca
    INNER JOIN @CargasSeleccionadas AS seleccionada
        ON seleccionada.CargaAcademicaId = ca.Id;

    IF (SELECT COUNT(*) FROM @Eliminadas)
       <> (SELECT COUNT(*) FROM @CargasSeleccionadas)
        THROW 51108, N'No se eliminaron todas las cargas seleccionadas. La transacción será revertida.', 1;

    COMMIT TRANSACTION;

    SELECT
        e.CargaAcademicaId,
        e.OfertaMateriaId,
        e.DocenteId,
        e.EstadoAnterior,
        e.EstabaEliminadaLogicamente
    FROM @Eliminadas AS e
    ORDER BY e.CargaAcademicaId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
