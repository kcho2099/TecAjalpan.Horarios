using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Services;

internal sealed class GeneracionHorariosBackgroundService(
    IColaGeneracionHorarios cola,
    IServiceScopeFactory scopeFactory,
    ILogger<GeneracionHorariosBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> RegistrarInterrumpidas =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(2102, nameof(GeneracionHorariosBackgroundService)),
            "Se marcaron {Cantidad} generaciones interrumpidas por reinicio");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecuperarPendientesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid ejecucionId;
            try
            {
                ejecucionId = await cola.DesencolarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var procesador = scope.ServiceProvider
                .GetRequiredService<ProcesadorGeneracionHorarios>();
            await procesador.ProcesarAsync(ejecucionId, stoppingToken);
        }
    }

    private async Task RecuperarPendientesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var interrumpidas = await dbContext.EjecucionesGenerador
            .Where(x => x.Estado == EstadoEjecucion.Ejecutando)
            .ToArrayAsync(cancellationToken);
        foreach (var ejecucion in interrumpidas)
        {
            ejecucion.Estado = EstadoEjecucion.Fallida;
            ejecucion.Fin = DateTime.UtcNow;
            ejecucion.Mensaje = "La generación fue interrumpida por un reinicio del servidor.";
        }

        var pendientes = await dbContext.EjecucionesGenerador.AsNoTracking()
            .Where(x => x.Estado == EstadoEjecucion.Pendiente)
            .OrderBy(x => x.FechaCrea)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var ejecucionId in pendientes)
            await cola.EncolarAsync(ejecucionId, cancellationToken);

        if (interrumpidas.Length > 0)
            RegistrarInterrumpidas(logger, interrumpidas.Length, null);
    }
}
