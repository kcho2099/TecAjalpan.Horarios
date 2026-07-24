using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Domain.Common;

namespace TecAjalpan.Horarios.Infrastructure.Persistence;

internal sealed class AuditoriaSaveChangesInterceptor(
    IUsuarioActual usuarioActual,
    IFechaHora fechaHora) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AplicarAuditoria(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AplicarAuditoria(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AplicarAuditoria(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var usuario = usuarioActual.UsuarioId ?? "sistema";
        foreach (var entry in context.ChangeTracker.Entries<EntidadAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.FechaCrea = fechaHora.UtcNow;
                entry.Entity.UsuarioCrea = usuario;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.FechaModifica = fechaHora.UtcNow;
                entry.Entity.UsuarioModifica = usuario;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.Eliminado = true;
                entry.Entity.FechaElimina = fechaHora.UtcNow;
                entry.Entity.UsuarioElimina = usuario;
            }
        }
    }
}

internal sealed class FechaHoraSistema : IFechaHora
{
    public DateTime UtcNow => DateTime.UtcNow;
}
