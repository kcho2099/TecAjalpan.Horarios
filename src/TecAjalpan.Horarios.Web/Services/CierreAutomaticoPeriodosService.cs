using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Services;

public sealed class CierreAutomaticoPeriodosService(
    IServiceScopeFactory scopeFactory,
    ILogger<CierreAutomaticoPeriodosService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CerrarVencidosAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CerrarVencidosAsync(stoppingToken);
        }
    }

    private async Task CerrarVencidosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var vencidos = await dbContext.Periodos
                .Where(x => !x.Eliminado
                    && x.Estado == EstadoPeriodo.Activo
                    && x.FechaFin < hoy)
                .ToArrayAsync(cancellationToken);

            if (vencidos.Length == 0)
            {
                return;
            }

            foreach (var vencido in vencidos)
            {
                vencido.Estado = EstadoPeriodo.Cerrado;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Se cerraron automáticamente {Cantidad} periodos vencidos.",
                vencidos.Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No fue posible cerrar automáticamente los periodos vencidos.");
        }
    }
}
