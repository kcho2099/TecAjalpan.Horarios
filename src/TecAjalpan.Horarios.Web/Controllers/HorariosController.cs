using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
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
    [HttpPost("generar")]
    public async Task<ActionResult<ResultadoGeneracionDto>> Generar(
        [FromBody] GenerarHorarioRequest request,
        CancellationToken cancellationToken)
    {
        var periodoExiste = await dbContext.Periodos.AsNoTracking()
            .AnyAsync(x => x.Id == request.PeriodoId, cancellationToken);
        if (!periodoExiste)
            return NotFound("No se encontró el periodo solicitado.");

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
}

public sealed record GenerarHorarioRequest(
    Guid PeriodoId,
    int TiempoLimiteSegundos = 60);

public sealed record ResultadoGeneracionDto(
    Guid HorarioVersionId,
    int NumeroVersion,
    bool Completa,
    int HorasSolicitadas,
    int HorasProgramadas,
    int SesionesGeneradas,
    IReadOnlyCollection<PendienteGeneracionDto> Pendientes);

public sealed record PendienteGeneracionDto(
    Guid CargaAcademicaId,
    byte Horas,
    string Codigo,
    string Detalle);
