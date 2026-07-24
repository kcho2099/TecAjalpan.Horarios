using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Domain.Entities;

namespace TecAjalpan.Horarios.Infrastructure.Persistence;

internal sealed class PeriodoRepository(ApplicationDbContext dbContext) : IPeriodoRepository
{
    public Task<Periodo?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Periodos
            .Include(x => x.Carreras)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Periodo>> ListarAsync(CancellationToken cancellationToken) =>
        await dbContext.Periodos
            .AsNoTracking()
            .OrderByDescending(x => x.FechaInicio)
            .ToListAsync(cancellationToken);

    public Task AgregarAsync(Periodo periodo, CancellationToken cancellationToken) =>
        dbContext.Periodos.AddAsync(periodo, cancellationToken).AsTask();

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class HorarioRepository(ApplicationDbContext dbContext) : IHorarioRepository
{
    public Task<HorarioVersion?> ObtenerVersionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.HorariosVersiones
            .Include(x => x.Sesiones)
            .Include(x => x.Pendientes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<int> SiguienteNumeroVersionAsync(
        Guid periodoId,
        Guid? periodoCarreraId,
        CancellationToken cancellationToken)
    {
        var ultimo = await dbContext.HorariosVersiones
            .Where(x => x.PeriodoId == periodoId && x.PeriodoCarreraId == periodoCarreraId)
            .MaxAsync(x => (int?)x.Numero, cancellationToken);
        return (ultimo ?? 0) + 1;
    }

    public Task AgregarVersionAsync(HorarioVersion version, CancellationToken cancellationToken) =>
        dbContext.HorariosVersiones.AddAsync(version, cancellationToken).AsTask();

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
