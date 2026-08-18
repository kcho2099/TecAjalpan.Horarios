using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Horarios;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/horarios")]
[Authorize(Policy = Politicas.GenerarHorario)]
public sealed class HorariosController(
    ApplicationDbContext dbContext,
    IGeneradorHorarios generador) : ControllerBase
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
            .Where(x => x.PeriodoId == periodoId && x.PeriodoCarreraId == null)
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
            .Where(x => x.Id == versionId)
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

    [HttpPost("generar")]
    public async Task<ActionResult<ResultadoGeneracionDto>> Generar(
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

        var solicitud = new SolicitudGeneracion(
            request.PeriodoId,
            null,
            Math.Clamp(request.TiempoLimiteSegundos, 1, 600),
            false);
        var ejecucion = new EjecucionGenerador
        {
            PeriodoId = request.PeriodoId,
            PeriodoCarreraId = null,
            Estado = EstadoEjecucion.Ejecutando,
            Inicio = DateTime.UtcNow,
            TiempoLimiteSegundos = solicitud.TiempoLimiteSegundos
        };
        dbContext.EjecucionesGenerador.Add(ejecucion);
        await dbContext.SaveChangesAsync(cancellationToken);

        ResultadoGeneracion resultado;
        try
        {
            resultado = await generador.GenerarAsync(solicitud, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ejecucion.Estado = EstadoEjecucion.Cancelada;
            ejecucion.Fin = DateTime.UtcNow;
            ejecucion.Mensaje = "La generación fue cancelada.";
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ejecucion.Estado = EstadoEjecucion.Fallida;
            ejecucion.Fin = DateTime.UtcNow;
            ejecucion.Mensaje = ex.Message.Length <= 1000
                ? ex.Message
                : ex.Message[..1000];
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        var ultimoNumero = await dbContext.HorariosVersiones
            .Where(x => x.PeriodoId == request.PeriodoId && x.PeriodoCarreraId == null)
            .MaxAsync(x => (int?)x.Numero, cancellationToken) ?? 0;
        var version = new HorarioVersion
        {
            PeriodoId = request.PeriodoId,
            PeriodoCarreraId = null,
            Numero = ultimoNumero + 1,
            Origen = "CP-SAT institucional"
        };

        foreach (var propuesta in resultado.Sesiones)
        {
            version.Sesiones.Add(new SesionHorario
            {
                CargaAcademicaId = propuesta.CargaAcademicaId,
                DocenteId = propuesta.DocenteId,
                GrupoId = propuesta.GrupoId,
                EspacioId = propuesta.EspacioId,
                Fecha = propuesta.Fecha,
                Dia = (DiaAcademico)propuesta.Dia,
                Bloque = propuesta.Bloque,
                DuracionBloques = 1,
                Origen = OrigenSesion.Automatica
            });
        }

        foreach (var pendiente in resultado.Pendientes)
        {
            version.Pendientes.Add(new PendienteGeneracion
            {
                CargaAcademicaId = pendiente.CargaAcademicaId,
                HorasPendientes = pendiente.Horas,
                Codigo = pendiente.Codigo,
                Detalle = pendiente.Detalle
            });
        }

        ejecucion.Estado = EstadoEjecucion.Completada;
        ejecucion.Fin = DateTime.UtcNow;
        ejecucion.HorasSolicitadas = resultado.HorasSolicitadas;
        ejecucion.HorasProgramadas = resultado.HorasProgramadas;
        ejecucion.Mensaje = resultado.Completa
            ? "Generación institucional completa."
            : $"Generación parcial con {resultado.Pendientes.Count} carga(s) pendiente(s).";
        dbContext.HorariosVersiones.Add(version);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            foreach (var sesion in version.Sesiones)
                dbContext.Entry(sesion).State = EntityState.Detached;
            foreach (var pendiente in version.Pendientes)
                dbContext.Entry(pendiente).State = EntityState.Detached;
            dbContext.Entry(version).State = EntityState.Detached;
            ejecucion.Estado = EstadoEjecucion.Fallida;
            ejecucion.Fin = DateTime.UtcNow;
            ejecucion.Mensaje = "Otra generación creó una versión simultáneamente.";
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return Conflict(
                "No se pudo guardar la versión porque otra generación modificó el periodo. Intenta nuevamente.");
        }

        return Ok(new ResultadoGeneracionDto(
            version.Id,
            version.Numero,
            resultado.Completa,
            resultado.HorasSolicitadas,
            resultado.HorasProgramadas,
            resultado.Sesiones.Count,
            resultado.Pendientes.Select(x => new PendienteGeneracionDto(
                x.CargaAcademicaId,
                x.Horas,
                x.Codigo,
                x.Detalle)).ToArray()));
    }

    private static string TextoEstado(EstadoHorario estado) => estado switch
    {
        EstadoHorario.Borrador => "Borrador",
        EstadoHorario.EnRevision => "En revisión",
        EstadoHorario.Aprobado => "Aprobado",
        EstadoHorario.Publicado => "Publicado",
        EstadoHorario.Reemplazado => "Reemplazado",
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
