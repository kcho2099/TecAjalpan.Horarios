using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Horarios;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;
using TecAjalpan.Horarios.Web.Services;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/horarios")]
[Authorize(Policy = Politicas.GenerarHorario)]
public sealed class HorariosController(
    ApplicationDbContext dbContext,
    IColaGeneracionHorarios colaGeneracion) : ControllerBase
{
    [HttpGet("periodos")]
    public async Task<ActionResult<IReadOnlyCollection<PeriodoGeneracionDto>>> Periodos(
        CancellationToken cancellationToken)
    {
        var periodos = await dbContext.Periodos.AsNoTracking()
            .Where(x => x.Estado != EstadoPeriodo.Cerrado)
            .OrderByDescending(x => x.Estado == EstadoPeriodo.Activo)
            .ThenByDescending(x => x.FechaInicio)
            .Select(x => new PeriodoGeneracionDto(
                x.Id,
                x.Nombre,
                (byte)x.Estado,
                x.Estado == EstadoPeriodo.Activo ? "Activo" : "Configuración"))
            .ToArrayAsync(cancellationToken);
        return Ok(periodos);
    }

    [HttpGet("periodos/{periodoId:guid}/versiones")]
    public async Task<ActionResult<IReadOnlyCollection<HorarioVersionResumenDto>>> Versiones(
        Guid periodoId,
        CancellationToken cancellationToken)
    {
        var versiones = await dbContext.HorariosVersiones.AsNoTracking()
            .Where(x => x.PeriodoId == periodoId
                && x.PeriodoCarreraId == null
                && x.Estado != EstadoHorario.Descartado)
            .OrderByDescending(x => x.Numero)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                x.Numero,
                x.Estado,
                x.Origen,
                x.FechaCrea,
                SesionesGeneradas = x.Sesiones.Count,
                NumeroPendientes = x.Pendientes.Count,
                HorasPendientes = x.Pendientes.Sum(p => (int?)p.HorasPendientes) ?? 0
            })
            .ToArrayAsync(cancellationToken);

        var respuesta = new List<HorarioVersionResumenDto>(versiones.Length);
        foreach (var version in versiones)
        {
            var horasProgramadas = await dbContext.SesionesHorario.AsNoTracking()
                .Where(x => x.HorarioVersionId == version.Id)
                .Select(x => new { x.CargaAcademicaId, x.Dia, x.Bloque, x.EspacioId })
                .Distinct()
                .CountAsync(cancellationToken);

            respuesta.Add(new HorarioVersionResumenDto(
                version.Id,
                version.Numero,
                (byte)version.Estado,
                TextoEstado(version.Estado),
                version.Origen,
                version.FechaCrea,
                horasProgramadas + version.HorasPendientes,
                horasProgramadas,
                version.SesionesGeneradas,
                version.NumeroPendientes,
                version.NumeroPendientes == 0));
        }

        return Ok(respuesta);
    }

    [HttpGet("versiones/{versionId:guid}")]
    public async Task<ActionResult<HorarioDetalleDto>> Version(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.HorariosVersiones.AsNoTracking()
            .Where(x => x.Id == versionId && x.Estado != EstadoHorario.Descartado)
            .Select(x => new
            {
                x.Id,
                x.PeriodoId,
                Periodo = x.Periodo.Nombre,
                x.Numero,
                x.Estado,
                x.Origen,
                x.FechaCrea
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
            return NotFound("No se encontró la versión de horario solicitada.");

        var sesiones = await dbContext.SesionesHorario.AsNoTracking()
            .Where(x => x.HorarioVersionId == versionId)
            .Select(x => new
            {
                x.CargaAcademicaId,
                CarreraId = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.CarreraId,
                Carrera = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.Carrera.Nombre,
                ModalidadId = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.ModalidadId,
                Modalidad = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.Modalidad.Nombre,
                x.GrupoId,
                Grupo = x.CargaAcademica.OfertaMateria.Grupo.Clave,
                MateriaClave = x.CargaAcademica.OfertaMateria.Materia.Clave,
                Materia = x.CargaAcademica.OfertaMateria.Materia.Nombre,
                x.DocenteId,
                DocenteNombres = x.CargaAcademica.Docente.Nombres,
                DocenteApellidos = x.CargaAcademica.Docente.Apellidos,
                EspacioClave = x.Espacio.Clave,
                Espacio = x.Espacio.Nombre,
                x.Dia,
                x.Bloque,
                x.Fecha
            })
            .ToArrayAsync(cancellationToken);

        var sesionesResumidas = sesiones
            .GroupBy(x => new
            {
                x.CargaAcademicaId,
                x.CarreraId,
                x.Carrera,
                x.ModalidadId,
                x.Modalidad,
                x.GrupoId,
                x.Grupo,
                x.MateriaClave,
                x.Materia,
                x.DocenteId,
                x.DocenteNombres,
                x.DocenteApellidos,
                x.EspacioClave,
                x.Espacio,
                x.Dia,
                x.Bloque
            })
            .Select(g => new HorarioSesionResumenDto(
                g.Key.CargaAcademicaId,
                g.Key.CarreraId,
                g.Key.Carrera,
                g.Key.ModalidadId,
                g.Key.Modalidad,
                g.Key.GrupoId,
                g.Key.Grupo,
                g.Key.MateriaClave,
                g.Key.Materia,
                g.Key.DocenteId,
                $"{g.Key.DocenteApellidos}, {g.Key.DocenteNombres}",
                $"{g.Key.EspacioClave} · {g.Key.Espacio}",
                (byte)g.Key.Dia,
                TextoDia(g.Key.Dia),
                g.Key.Bloque,
                HoraDeBloque(g.Key.Bloque),
                HoraDeBloque(g.Key.Bloque + 1),
                g.Min(x => x.Fecha),
                g.Max(x => x.Fecha),
                g.Count()))
            .OrderBy(x => x.Carrera)
            .ThenBy(x => x.Grupo)
            .ThenBy(x => x.Dia)
            .ThenBy(x => x.Bloque)
            .ToArray();

        var pendientes = await dbContext.PendientesGeneracion.AsNoTracking()
            .Where(x => x.HorarioVersionId == versionId)
            .Select(x => new PendienteGeneracionDto(
                x.CargaAcademicaId,
                x.HorasPendientes,
                x.Codigo,
                x.Detalle))
            .ToArrayAsync(cancellationToken);

        return Ok(new HorarioDetalleDto(
            version.Id,
            version.PeriodoId,
            version.Periodo,
            version.Numero,
            (byte)version.Estado,
            TextoEstado(version.Estado),
            version.Origen,
            version.FechaCrea,
            sesionesResumidas,
            pendientes));
    }

    [HttpGet("versiones/{versionId:guid}/excel")]
    public async Task<IActionResult> ExportarExcel(
        Guid versionId,
        [FromQuery] Guid? carreraId,
        [FromQuery] Guid? modalidadId,
        [FromQuery] Guid? grupoId,
        [FromQuery] Guid? docenteId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.HorariosVersiones.AsNoTracking()
            .Where(x => x.Id == versionId && x.Estado != EstadoHorario.Descartado)
            .Select(x => new { Periodo = x.Periodo.Nombre, x.Numero })
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
            return NotFound("No se encontró la versión de horario solicitada.");

        var filas = await ConsultarFilasExportacionAsync(
            versionId,
            carreraId,
            modalidadId,
            grupoId,
            docenteId,
            cancellationToken);

        var archivo = ExportadorHorarioExcel.Crear(
            version.Periodo, version.Numero, filas);
        return File(
            archivo,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"horario-{version.Numero}-{DateTime.UtcNow:yyyyMMddHHmm}.xlsx");
    }

    [HttpGet("versiones/{versionId:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(
        Guid versionId,
        [FromQuery] Guid? carreraId,
        [FromQuery] Guid? modalidadId,
        [FromQuery] Guid? grupoId,
        [FromQuery] Guid? docenteId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.HorariosVersiones.AsNoTracking()
            .Where(x => x.Id == versionId && x.Estado != EstadoHorario.Descartado)
            .Select(x => new
            {
                Periodo = x.Periodo.Nombre,
                x.Numero,
                x.Estado,
                Pendientes = x.Pendientes.Count
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
            return NotFound("No se encontró la versión de horario solicitada.");

        var filas = await ConsultarFilasExportacionAsync(
            versionId,
            carreraId,
            modalidadId,
            grupoId,
            docenteId,
            cancellationToken);
        var archivo = ExportadorHorarioPdf.Crear(
            version.Periodo,
            version.Numero,
            TextoEstado(version.Estado),
            version.Pendientes,
            filas,
            grupoId,
            docenteId);
        return File(
            archivo,
            "application/pdf",
            $"horario-borrador-v{version.Numero}-{DateTime.UtcNow:yyyyMMddHHmm}.pdf");
    }

    private async Task<HorarioSesionResumenDto[]> ConsultarFilasExportacionAsync(
        Guid versionId,
        Guid? carreraId,
        Guid? modalidadId,
        Guid? grupoId,
        Guid? docenteId,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.SesionesHorario.AsNoTracking()
            .Where(x => x.HorarioVersionId == versionId);
        if (carreraId.HasValue)
        {
            consulta = consulta.Where(x =>
                x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.CarreraId == carreraId.Value);
        }
        if (modalidadId.HasValue)
        {
            consulta = consulta.Where(x =>
                x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.ModalidadId == modalidadId.Value);
        }
        if (grupoId.HasValue)
            consulta = consulta.Where(x => x.GrupoId == grupoId.Value);
        if (docenteId.HasValue)
            consulta = consulta.Where(x => x.DocenteId == docenteId.Value);

        var sesiones = await consulta.Select(x => new
            {
                x.CargaAcademicaId,
                CarreraId = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.CarreraId,
                Carrera = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.Carrera.Nombre,
                ModalidadId = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.ModalidadId,
                Modalidad = x.CargaAcademica.OfertaMateria.Grupo.PeriodoCarrera.Modalidad.Nombre,
                x.GrupoId,
                Grupo = x.CargaAcademica.OfertaMateria.Grupo.Clave,
                MateriaClave = x.CargaAcademica.OfertaMateria.Materia.Clave,
                Materia = x.CargaAcademica.OfertaMateria.Materia.Nombre,
                x.DocenteId,
                DocenteNombres = x.CargaAcademica.Docente.Nombres,
                DocenteApellidos = x.CargaAcademica.Docente.Apellidos,
                EspacioClave = x.Espacio.Clave,
                Espacio = x.Espacio.Nombre,
                x.Dia,
                x.Bloque,
                x.Fecha
            })
            .ToArrayAsync(cancellationToken);
        return sesiones
            .GroupBy(x => new
            {
                x.CargaAcademicaId,
                x.CarreraId,
                x.Carrera,
                x.ModalidadId,
                x.Modalidad,
                x.GrupoId,
                x.Grupo,
                x.MateriaClave,
                x.Materia,
                x.DocenteId,
                x.DocenteNombres,
                x.DocenteApellidos,
                x.EspacioClave,
                x.Espacio,
                x.Dia,
                x.Bloque
            })
            .Select(g => new HorarioSesionResumenDto(
                g.Key.CargaAcademicaId,
                g.Key.CarreraId,
                g.Key.Carrera,
                g.Key.ModalidadId,
                g.Key.Modalidad,
                g.Key.GrupoId,
                g.Key.Grupo,
                g.Key.MateriaClave,
                g.Key.Materia,
                g.Key.DocenteId,
                $"{g.Key.DocenteApellidos}, {g.Key.DocenteNombres}",
                $"{g.Key.EspacioClave} · {g.Key.Espacio}",
                (byte)g.Key.Dia,
                TextoDia(g.Key.Dia),
                g.Key.Bloque,
                HoraDeBloque(g.Key.Bloque),
                HoraDeBloque(g.Key.Bloque + 1),
                g.Min(x => x.Fecha),
                g.Max(x => x.Fecha),
                g.Count()))
            .OrderBy(x => x.Carrera)
            .ThenBy(x => x.Grupo)
            .ThenBy(x => x.Dia)
            .ThenBy(x => x.Bloque)
            .ToArray();
    }

    [HttpPost("generar")]
    public async Task<ActionResult<InicioGeneracionDto>> Generar(
        [FromBody] GenerarHorarioRequest request,
        CancellationToken cancellationToken)
    {
        var estadoPeriodo = await dbContext.Periodos.AsNoTracking()
            .Where(x => x.Id == request.PeriodoId)
            .Select(x => (EstadoPeriodo?)x.Estado)
            .SingleOrDefaultAsync(cancellationToken);
        if (!estadoPeriodo.HasValue)
            return NotFound("No se encontró el periodo solicitado.");
        if (estadoPeriodo == EstadoPeriodo.Cerrado)
            return Conflict("No se puede generar una nueva versión para un periodo cerrado.");

        var existeActiva = await dbContext.EjecucionesGenerador.AsNoTracking()
            .AnyAsync(x => x.PeriodoId == request.PeriodoId
                && x.PeriodoCarreraId == null
                && (x.Estado == EstadoEjecucion.Pendiente
                    || x.Estado == EstadoEjecucion.Ejecutando),
                cancellationToken);
        if (existeActiva)
            return Conflict("Ya existe una generación en proceso para este periodo.");

        var ejecucion = new EjecucionGenerador
        {
            PeriodoId = request.PeriodoId,
            PeriodoCarreraId = null,
            Estado = EstadoEjecucion.Pendiente,
            TiempoLimiteSegundos = Math.Clamp(request.TiempoLimiteSegundos, 10, 600),
            Mensaje = "La generación está en espera para comenzar."
        };
        dbContext.EjecucionesGenerador.Add(ejecucion);
        await dbContext.SaveChangesAsync(cancellationToken);
        await colaGeneracion.EncolarAsync(ejecucion.Id, CancellationToken.None);

        return AcceptedAtAction(
            nameof(ConsultarEjecucion),
            new { ejecucionId = ejecucion.Id },
            new InicioGeneracionDto(
                ejecucion.Id,
                (byte)ejecucion.Estado,
                TextoEstadoEjecucion(ejecucion.Estado)));
    }

    [HttpGet("ejecuciones/{ejecucionId:guid}")]
    public async Task<ActionResult<EstadoGeneracionDto>> ConsultarEjecucion(
        Guid ejecucionId,
        CancellationToken cancellationToken)
    {
        var estado = await CrearEstadoEjecucionAsync(ejecucionId, cancellationToken);
        return estado is null
            ? NotFound("No se encontró la ejecución solicitada.")
            : Ok(estado);
    }

    [HttpGet("periodos/{periodoId:guid}/ejecucion-activa")]
    public async Task<ActionResult<EstadoGeneracionDto>> EjecucionActiva(
        Guid periodoId,
        CancellationToken cancellationToken)
    {
        var ejecucionId = await dbContext.EjecucionesGenerador.AsNoTracking()
            .Where(x => x.PeriodoId == periodoId
                && x.PeriodoCarreraId == null
                && (x.Estado == EstadoEjecucion.Pendiente
                    || x.Estado == EstadoEjecucion.Ejecutando))
            .OrderByDescending(x => x.FechaCrea)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!ejecucionId.HasValue)
            return NoContent();

        return Ok(await CrearEstadoEjecucionAsync(ejecucionId.Value, cancellationToken));
    }

    [HttpPost("versiones/{versionId:guid}/descartar")]
    public async Task<IActionResult> Descartar(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.HorariosVersiones
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound("No se encontró la versión de horario solicitada.");
        if (version.Estado != EstadoHorario.Borrador)
            return Conflict("Únicamente se pueden descartar versiones en borrador.");

        version.Descartar();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(
                "La versión fue modificada por otro usuario. Actualiza la pantalla e intenta nuevamente.");
        }

        return Ok();
    }

    private async Task<EstadoGeneracionDto?> CrearEstadoEjecucionAsync(
        Guid ejecucionId,
        CancellationToken cancellationToken)
    {
        var ejecucion = await dbContext.EjecucionesGenerador.AsNoTracking()
            .Where(x => x.Id == ejecucionId)
            .Select(x => new
            {
                x.Id,
                x.Estado,
                x.Mensaje,
                x.TiempoLimiteSegundos,
                x.Inicio,
                x.Fin,
                x.HorasSolicitadas,
                x.HorasProgramadas,
                x.HorarioVersionId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (ejecucion is null)
            return null;

        ResultadoGeneracionDto? resultado = null;
        if (ejecucion.Estado == EstadoEjecucion.Completada
            && ejecucion.HorarioVersionId.HasValue)
        {
            var version = await dbContext.HorariosVersiones.AsNoTracking()
                .Where(x => x.Id == ejecucion.HorarioVersionId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Numero,
                    SesionesGeneradas = x.Sesiones.Count
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (version is not null)
            {
                var pendientes = await dbContext.PendientesGeneracion.AsNoTracking()
                    .Where(x => x.HorarioVersionId == version.Id)
                    .Select(x => new PendienteGeneracionDto(
                        x.CargaAcademicaId,
                        x.HorasPendientes,
                        x.Codigo,
                        x.Detalle))
                    .ToArrayAsync(cancellationToken);
                resultado = new ResultadoGeneracionDto(
                    version.Id,
                    version.Numero,
                    pendientes.Length == 0,
                    ejecucion.HorasSolicitadas,
                    ejecucion.HorasProgramadas,
                    version.SesionesGeneradas,
                    pendientes);
            }
        }

        return new EstadoGeneracionDto(
            ejecucion.Id,
            (byte)ejecucion.Estado,
            TextoEstadoEjecucion(ejecucion.Estado),
            ejecucion.Mensaje,
            ejecucion.TiempoLimiteSegundos,
            ejecucion.Inicio,
            ejecucion.Fin,
            resultado);
    }

    private static string TextoEstado(EstadoHorario estado) => estado switch
    {
        EstadoHorario.Borrador => "Borrador",
        EstadoHorario.EnRevision => "En revisión",
        EstadoHorario.Aprobado => "Aprobado",
        EstadoHorario.Publicado => "Publicado",
        EstadoHorario.Reemplazado => "Reemplazado",
        EstadoHorario.Descartado => "Descartado",
        _ => estado.ToString()
    };

    private static string TextoEstadoEjecucion(EstadoEjecucion estado) => estado switch
    {
        EstadoEjecucion.Pendiente => "En espera",
        EstadoEjecucion.Ejecutando => "Generando",
        EstadoEjecucion.Completada => "Completada",
        EstadoEjecucion.Fallida => "Fallida",
        EstadoEjecucion.Cancelada => "Cancelada",
        _ => estado.ToString()
    };

    private static string TextoDia(DiaAcademico dia) => dia switch
    {
        DiaAcademico.Lunes => "Lunes",
        DiaAcademico.Martes => "Martes",
        DiaAcademico.Miercoles => "Miércoles",
        DiaAcademico.Jueves => "Jueves",
        DiaAcademico.Viernes => "Viernes",
        DiaAcademico.Sabado => "Sábado",
        _ => dia.ToString()
    };

    private static string HoraDeBloque(int bloque) =>
        $"{Math.Clamp(7 + bloque, 0, 23):00}:00";
}
