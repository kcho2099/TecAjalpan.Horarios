using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Services;

internal sealed class ProcesadorGeneracionHorarios(
    ApplicationDbContext dbContext,
    IGeneradorHorarios generador,
    ILogger<ProcesadorGeneracionHorarios> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> RegistrarFallo =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2101, nameof(ProcesadorGeneracionHorarios)),
            "Falló la ejecución de horarios {EjecucionId}");

    public async Task ProcesarAsync(
        Guid ejecucionId,
        CancellationToken cancellationToken)
    {
        var ejecucion = await dbContext.EjecucionesGenerador
            .SingleOrDefaultAsync(x => x.Id == ejecucionId, cancellationToken);
        if (ejecucion is null || ejecucion.Estado != EstadoEjecucion.Pendiente)
            return;

        ejecucion.Estado = EstadoEjecucion.Ejecutando;
        ejecucion.Inicio = DateTime.UtcNow;
        ejecucion.Mensaje = "El motor está evaluando y compactando el horario.";
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var solicitud = new SolicitudGeneracion(
                ejecucion.PeriodoId,
                ejecucion.PeriodoCarreraId,
                Math.Clamp(ejecucion.TiempoLimiteSegundos, 1, 600),
                false);
            var resultado = await generador.GenerarAsync(solicitud, cancellationToken);
            var ultimoNumero = await dbContext.HorariosVersiones
                .Where(x => x.PeriodoId == ejecucion.PeriodoId
                    && x.PeriodoCarreraId == ejecucion.PeriodoCarreraId)
                .MaxAsync(x => (int?)x.Numero, cancellationToken) ?? 0;
            var version = CrearVersion(ejecucion, resultado, ultimoNumero + 1);

            ejecucion.HorarioVersionId = version.Id;
            ejecucion.Estado = EstadoEjecucion.Completada;
            ejecucion.Fin = DateTime.UtcNow;
            ejecucion.HorasSolicitadas = resultado.HorasSolicitadas;
            ejecucion.HorasProgramadas = resultado.HorasProgramadas;
            ejecucion.Mensaje = resultado.Completa
                ? "Generación institucional completa."
                : $"Generación parcial con {resultado.Pendientes.Count} carga(s) pendiente(s).";
            dbContext.HorariosVersiones.Add(version);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await MarcarFinalAsync(
                ejecucionId,
                EstadoEjecucion.Cancelada,
                "La generación fue cancelada porque el servidor se detuvo.");
        }
        catch (DatosGeneracionInvalidosException ex)
        {
            await MarcarFinalAsync(ejecucionId, EstadoEjecucion.Fallida, ex.Message);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await MarcarFinalAsync(
                ejecucionId,
                EstadoEjecucion.Fallida,
                "Otra generación creó una versión simultáneamente. Intenta nuevamente.");
        }
        catch (Exception ex)
        {
            RegistrarFallo(logger, ejecucionId, ex);
            await MarcarFinalAsync(ejecucionId, EstadoEjecucion.Fallida, ex.Message);
        }
    }

    private static HorarioVersion CrearVersion(
        EjecucionGenerador ejecucion,
        ResultadoGeneracion resultado,
        int numero)
    {
        var version = new HorarioVersion
        {
            PeriodoId = ejecucion.PeriodoId,
            PeriodoCarreraId = ejecucion.PeriodoCarreraId,
            Numero = numero,
            Origen = "CP-SAT escolarizado + módulos sabatinos fijos"
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
                Origen = propuesta.EsFija ? OrigenSesion.Manual : OrigenSesion.Automatica,
                FijadaParaRegeneracion = propuesta.EsFija
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

        return version;
    }

    private async Task MarcarFinalAsync(
        Guid ejecucionId,
        EstadoEjecucion estado,
        string mensaje)
    {
        dbContext.ChangeTracker.Clear();
        var ejecucion = await dbContext.EjecucionesGenerador
            .SingleOrDefaultAsync(x => x.Id == ejecucionId, CancellationToken.None);
        if (ejecucion is null)
            return;

        ejecucion.Estado = estado;
        ejecucion.Fin = DateTime.UtcNow;
        ejecucion.Mensaje = mensaje.Length <= 1000 ? mensaje : mensaje[..1000];
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }
}
