/*
    Vista de diagnóstico para las cargas sabatinas de docentes.

    Fuentes de la carga y del módulo:
      Operacion.CargasAcademicas
      Academico.OfertasMaterias
      Academico.Grupos
      Academico.PeriodosCarreras
      Catalogos.Modalidades
      Sabatino.ModulosMaterias
      Sabatino.ModulosSabatinos
      Sabatino.ConfiguracionesSabatinas

    Fuentes descriptivas y de disponibilidad:
      Recursos.Docentes
      Recursos.DisponibilidadesDocentes
      Recursos.JornadasDocentes
      Academico.Periodos
      Catalogos.Carreras
      Catalogos.Materias

    EstadoCarga: 1 = Borrador, 2 = Autorizada, 3 = Devuelta.
    TipoModalidad: 1 = Escolarizada, 2 = Sabatina.
    TurnoSabatino: 1 = Matutino, 2 = Vespertino.
*/

SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW Operacion.vw_DiagnosticoCargaDocenteSabatina
AS
    SELECT
        ca.Id AS CargaAcademicaId,
        ca.DocenteId,
        d.NumeroTrabajador,
        CONCAT(d.Apellidos, N', ', d.Nombres) AS Docente,
        d.Activo AS DocenteActivo,
        d.Eliminado AS DocenteEliminado,

        ca.Estado AS EstadoCargaId,
        CASE ca.Estado
            WHEN 1 THEN N'Borrador'
            WHEN 2 THEN N'Autorizada'
            WHEN 3 THEN N'Devuelta'
            ELSE CONCAT(N'Desconocido (', ca.Estado, N')')
        END AS EstadoCarga,
        ca.Eliminado AS CargaEliminada,
        ca.FechaCrea AS FechaCreacionCarga,
        ca.FechaModifica AS FechaModificacionCarga,
        ca.FechaElimina AS FechaEliminacionCarga,
        ca.UsuarioCrea AS UsuarioCreacionCarga,
        ca.UsuarioModifica AS UsuarioModificacionCarga,
        ca.UsuarioElimina AS UsuarioEliminacionCarga,

        p.Id AS PeriodoId,
        p.Nombre AS Periodo,
        p.Eliminado AS PeriodoEliminado,
        pc.Id AS PeriodoCarreraId,
        pc.Eliminado AS PeriodoCarreraEliminado,
        c.Id AS CarreraId,
        c.Clave AS CarreraClave,
        c.Nombre AS Carrera,
        c.Activo AS CarreraActiva,
        c.Eliminado AS CarreraEliminada,
        md.Id AS ModalidadId,
        md.Tipo AS TipoModalidadId,
        CASE md.Tipo
            WHEN 1 THEN N'Escolarizada'
            WHEN 2 THEN N'Sabatina'
            ELSE CONCAT(N'Desconocida (', md.Tipo, N')')
        END AS Modalidad,
        md.Activo AS ModalidadActiva,
        md.Eliminado AS ModalidadEliminada,

        g.Id AS GrupoId,
        g.Clave AS GrupoClave,
        g.Nombre AS Grupo,
        g.Semestre,
        g.Eliminado AS GrupoEliminado,
        om.Id AS OfertaMateriaId,
        om.Activa AS OfertaActiva,
        om.Eliminado AS OfertaEliminada,
        om.HorasRequeridas,
        m.Id AS MateriaId,
        m.Clave AS MateriaClave,
        m.Nombre AS Materia,
        m.Activo AS MateriaActiva,
        m.Eliminado AS MateriaEliminada,

        mm.Id AS ModuloMateriaId,
        mm.Eliminado AS ModuloMateriaEliminado,
        ms.Id AS ModuloSabatinoId,
        ms.Orden AS Modulo,
        ms.Semanas,
        ms.FechaInicio,
        ms.FechaFin,
        ms.Eliminado AS ModuloSabatinoEliminado,
        cs.Id AS ConfiguracionSabatinaId,
        cs.Validada AS ConfiguracionSabatinaValidada,
        cs.Eliminado AS ConfiguracionSabatinaEliminada,
        mm.Turno AS TurnoId,
        CASE mm.Turno
            WHEN 1 THEN N'Matutino'
            WHEN 2 THEN N'Vespertino'
            ELSE N'Sin turno sabatino'
        END AS Turno,
        CASE mm.Turno
            WHEN 1 THEN CAST('08:00' AS time(0))
            WHEN 2 THEN CAST('12:00' AS time(0))
        END AS HoraInicio,
        CASE mm.Turno
            WHEN 1 THEN CAST('12:00' AS time(0))
            WHEN 2 THEN CAST('16:00' AS time(0))
        END AS HoraFin,

        dd.Id AS DisponibilidadDocenteId,
        dd.Validada AS DisponibilidadValidada,
        dd.Eliminado AS DisponibilidadEliminada,
        jd.Id AS JornadaSabatinaId,
        jd.HoraInicio AS DisponibilidadHoraInicio,
        jd.HoraFin AS DisponibilidadHoraFin,
        jd.Eliminado AS JornadaSabatinaEliminada,

        /* Reproduce las condiciones con las que el backend arma hoy
           CargaSabatinaModulos. Deliberadamente no filtra EstadoCarga. */
        CONVERT(bit, CASE WHEN
            ca.Eliminado = 0
            AND d.Eliminado = 0
            AND d.Activo = 1
            AND om.Eliminado = 0
            AND om.Activa = 1
            AND g.Eliminado = 0
            AND pc.Eliminado = 0
            AND p.Eliminado = 0
            AND md.Eliminado = 0
            AND md.Tipo = 2
            AND mm.Id IS NOT NULL
            AND mm.Eliminado = 0
            AND ms.Id IS NOT NULL
            AND ms.Eliminado = 0
            AND cs.Id IS NOT NULL
            AND cs.Eliminado = 0
            THEN 1 ELSE 0 END) AS ContadaActualmentePorBackend,

        /* Regla correcta para que una carga bloquee una franja:
           solamente Borrador o Autorizada, nunca Devuelta. */
        CONVERT(bit, CASE WHEN
            ca.Estado IN (1, 2)
            AND ca.Eliminado = 0
            AND d.Eliminado = 0
            AND d.Activo = 1
            AND om.Eliminado = 0
            AND om.Activa = 1
            AND g.Eliminado = 0
            AND pc.Eliminado = 0
            AND p.Eliminado = 0
            AND md.Eliminado = 0
            AND md.Activo = 1
            AND md.Tipo = 2
            AND mm.Id IS NOT NULL
            AND mm.Eliminado = 0
            AND ms.Id IS NOT NULL
            AND ms.Eliminado = 0
            AND cs.Id IS NOT NULL
            AND cs.Eliminado = 0
            THEN 1 ELSE 0 END) AS DebeParticiparEnCruce,

        CASE
            WHEN ca.Estado = 3 AND ca.Eliminado = 0
                THEN N'ANOMALIA: carga Devuelta no eliminada; el backend actual la cuenta'
            WHEN ca.Eliminado = 1
                THEN N'No vigente: carga eliminada'
            WHEN d.Eliminado = 1 OR d.Activo = 0
                THEN N'No vigente: docente eliminado o inactivo'
            WHEN md.Tipo <> 2
                THEN N'No es carga sabatina'
            WHEN om.Eliminado = 1 OR om.Activa = 0
                THEN N'No vigente: oferta eliminada o inactiva'
            WHEN g.Eliminado = 1 OR pc.Eliminado = 1 OR p.Eliminado = 1
                THEN N'No vigente: cadena académica eliminada'
            WHEN mm.Id IS NULL OR ms.Id IS NULL OR cs.Id IS NULL
                THEN N'ANOMALIA: carga sabatina sin módulo completo'
            WHEN mm.Eliminado = 1 OR ms.Eliminado = 1 OR cs.Eliminado = 1
                THEN N'No vigente: módulo o configuración eliminado'
            WHEN ca.Estado NOT IN (1, 2)
                THEN N'No vigente: estado de carga no operativo'
            ELSE N'Carga sabatina vigente'
        END AS Diagnostico
    FROM Operacion.CargasAcademicas AS ca
    INNER JOIN Recursos.Docentes AS d
        ON d.Id = ca.DocenteId
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
    LEFT JOIN Sabatino.ModulosMaterias AS mm
        ON mm.OfertaMateriaId = om.Id
    LEFT JOIN Sabatino.ModulosSabatinos AS ms
        ON ms.Id = mm.ModuloSabatinoId
    LEFT JOIN Sabatino.ConfiguracionesSabatinas AS cs
        ON cs.Id = ms.ConfiguracionSabatinaId
    LEFT JOIN Recursos.DisponibilidadesDocentes AS dd
        ON dd.DocenteId = d.Id
        AND dd.PeriodoId = p.Id
    LEFT JOIN Recursos.JornadasDocentes AS jd
        ON jd.DisponibilidadDocenteId = dd.Id
        AND jd.Dia = 6
        AND jd.EsSemanaSabatina = 1;
GO

/* 1) Todo lo que existe para Diego, incluso registros eliminados o devueltos. */
SELECT *
FROM Operacion.vw_DiagnosticoCargaDocenteSabatina
WHERE Docente LIKE N'%Diego Maldonado%'
   OR Docente LIKE N'%Maldonado, Raúl Alberto%'
ORDER BY Periodo, FechaInicio, HoraInicio, MateriaClave;
GO

/* 2) Sólo los registros que el backend está contando actualmente para Diego. */
SELECT
    CargaAcademicaId,
    EstadoCarga,
    CargaEliminada,
    Periodo,
    Carrera,
    Modalidad,
    GrupoClave,
    MateriaClave,
    Materia,
    Modulo,
    FechaInicio,
    FechaFin,
    Turno,
    HoraInicio,
    HoraFin,
    DebeParticiparEnCruce,
    Diagnostico
FROM Operacion.vw_DiagnosticoCargaDocenteSabatina
WHERE (Docente LIKE N'%Diego Maldonado%'
       OR Docente LIKE N'%Maldonado, Raúl Alberto%')
  AND ContadaActualmentePorBackend = 1
ORDER BY Periodo, FechaInicio, HoraInicio, MateriaClave;
GO

/* 3) Detecta pares de cargas sabatinas vigentes del mismo docente que realmente
      se cruzan: mismo periodo, misma franja y fechas traslapadas. */
SELECT
    a.Docente,
    a.Periodo,
    a.Carrera AS CarreraA,
    a.GrupoClave AS GrupoA,
    a.MateriaClave AS MateriaClaveA,
    a.Materia AS MateriaA,
    a.Modulo AS ModuloA,
    a.FechaInicio AS FechaInicioA,
    a.FechaFin AS FechaFinA,
    a.Turno,
    b.Carrera AS CarreraB,
    b.GrupoClave AS GrupoB,
    b.MateriaClave AS MateriaClaveB,
    b.Materia AS MateriaB,
    b.Modulo AS ModuloB,
    b.FechaInicio AS FechaInicioB,
    b.FechaFin AS FechaFinB
FROM Operacion.vw_DiagnosticoCargaDocenteSabatina AS a
INNER JOIN Operacion.vw_DiagnosticoCargaDocenteSabatina AS b
    ON b.DocenteId = a.DocenteId
    AND b.PeriodoId = a.PeriodoId
    AND b.TurnoId = a.TurnoId
    AND b.FechaInicio <= a.FechaFin
    AND b.FechaFin >= a.FechaInicio
    AND b.CargaAcademicaId > a.CargaAcademicaId
WHERE a.DebeParticiparEnCruce = 1
  AND b.DebeParticiparEnCruce = 1
  AND (a.Docente LIKE N'%Diego Maldonado%'
       OR a.Docente LIKE N'%Maldonado, Raúl Alberto%')
ORDER BY a.Periodo, a.FechaInicio, a.Turno, a.MateriaClave;
GO
