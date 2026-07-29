using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Infrastructure.Identity;

namespace TecAjalpan.Horarios.Infrastructure.Persistence;

internal sealed class AuditoriaSaveChangesInterceptor(
    IUsuarioActual usuarioActual,
    IFechaHora fechaHora) : SaveChangesInterceptor
{
    private static readonly HashSet<string> PropiedadesProtegidas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(EntidadAuditable.RowVersion),
            nameof(UsuarioAplicacion.PasswordHash),
            nameof(UsuarioAplicacion.SecurityStamp),
            nameof(UsuarioAplicacion.ConcurrencyStamp)
        };

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

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

        context.ChangeTracker.DetectChanges();
        var cambios = context.ChangeTracker.Entries()
            .Where(x => x.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted)
            .Select(x => new CambioPendiente(x, x.State))
            .ToArray();

        var usuario = usuarioActual.UsuarioId ?? "sistema";
        foreach (var cambio in cambios.Where(x => x.Entry.Entity is EntidadAuditable))
        {
            AplicarCamposAuditoria(cambio, usuario);
        }

        context.ChangeTracker.DetectChanges();
        var registros = cambios
            .Where(EsRegistrable)
            .Select(x => CrearRegistro(x, usuario))
            .Where(x => x is not null)
            .Cast<Bitacora>()
            .ToArray();

        if (registros.Length > 0)
        {
            context.Set<Bitacora>().AddRange(registros);
        }
    }

    private void AplicarCamposAuditoria(CambioPendiente cambio, string usuario)
    {
        var entidad = (EntidadAuditable)cambio.Entry.Entity;
        switch (cambio.EstadoOriginal)
        {
            case EntityState.Added:
                entidad.FechaCrea = fechaHora.UtcNow;
                entidad.UsuarioCrea = usuario;
                break;

            case EntityState.Modified:
                entidad.FechaModifica = fechaHora.UtcNow;
                entidad.UsuarioModifica = usuario;
                break;

            case EntityState.Deleted:
                cambio.Entry.State = EntityState.Modified;
                entidad.Eliminado = true;
                entidad.FechaElimina = fechaHora.UtcNow;
                entidad.UsuarioElimina = usuario;
                break;
        }
    }

    private static bool EsRegistrable(CambioPendiente cambio) =>
        cambio.Entry.Entity is EntidadAuditable
            or UsuarioAplicacion
            or UsuarioCarrera
            or IdentityUserRole<string>;

    private Bitacora? CrearRegistro(CambioPendiente cambio, string usuario)
    {
        var accion = ObtenerAccion(cambio);
        var anteriores = ObtenerValores(cambio, anteriores: true);
        var nuevos = ObtenerValores(cambio, anteriores: false);

        if (cambio.EstadoOriginal == EntityState.Modified
            && anteriores.Count == 0
            && nuevos.Count == 0
            && accion == "Modificacion")
        {
            return null;
        }

        return new Bitacora
        {
            Entidad = ObtenerNombreEntidad(cambio.Entry.Entity),
            RegistroId = ObtenerRegistroId(cambio.Entry),
            Accion = accion,
            UsuarioId = usuario,
            Fecha = fechaHora.UtcNow,
            ValoresAnteriores = anteriores.Count == 0
                ? null
                : JsonSerializer.Serialize(anteriores, OpcionesJson),
            ValoresNuevos = nuevos.Count == 0
                ? null
                : JsonSerializer.Serialize(nuevos, OpcionesJson),
            CorrelationId = Activity.Current?.TraceId.ToString()
        };
    }

    private static string ObtenerAccion(CambioPendiente cambio)
    {
        if (cambio.Entry.Entity is UsuarioCarrera or DocenteCarrera)
        {
            if (cambio.Entry.Entity is DocenteCarrera
                && cambio.EstadoOriginal == EntityState.Modified
                && PropiedadCambio(cambio.Entry, nameof(DocenteCarrera.EsPrincipal)))
            {
                return "CambioCarreraPrincipal";
            }

            return cambio.EstadoOriginal == EntityState.Deleted
                ? "RetiroCarrera"
                : "AsignacionCarrera";
        }

        if (cambio.Entry.Entity is IdentityUserRole<string>)
        {
            return cambio.EstadoOriginal == EntityState.Deleted
                ? "RetiroRol"
                : "AsignacionRol";
        }

        if (cambio.EstadoOriginal == EntityState.Added)
        {
            return "Alta";
        }

        if (cambio.EstadoOriginal == EntityState.Deleted)
        {
            return "Baja";
        }

        if (PropiedadCambio(cambio.Entry, nameof(UsuarioAplicacion.PasswordHash)))
        {
            return "RestablecimientoContrasena";
        }

        if (PropiedadCambio(cambio.Entry, "Activo"))
        {
            return Convert.ToBoolean(
                cambio.Entry.Property("Activo").CurrentValue,
                CultureInfo.InvariantCulture)
                ? "Activacion"
                : "Desactivacion";
        }

        if (cambio.Entry.Entity is Periodo
            && PropiedadCambio(cambio.Entry, nameof(Periodo.Estado)))
        {
            var estadoAnterior =
                cambio.Entry.Property(nameof(Periodo.Estado)).OriginalValue?.ToString();
            var estadoNuevo =
                cambio.Entry.Property(nameof(Periodo.Estado)).CurrentValue?.ToString();

            if (string.Equals(estadoAnterior, "Cerrado", StringComparison.Ordinal))
            {
                return "Reapertura";
            }

            return string.Equals(estadoNuevo, "Cerrado", StringComparison.Ordinal)
                ? "Cierre"
                : "CambioEstado";
        }

        return "Modificacion";
    }

    private static Dictionary<string, object?> ObtenerValores(
        CambioPendiente cambio,
        bool anteriores)
    {
        var resultado = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var propiedad in cambio.Entry.Properties)
        {
            if (PropiedadesProtegidas.Contains(propiedad.Metadata.Name))
            {
                continue;
            }

            var incluir = cambio.EstadoOriginal switch
            {
                EntityState.Added => !anteriores,
                EntityState.Deleted => true,
                EntityState.Modified => PropiedadCambio(
                    cambio.Entry,
                    propiedad.Metadata.Name),
                _ => false
            };

            if (!incluir)
            {
                continue;
            }

            resultado[propiedad.Metadata.Name] = anteriores
                ? propiedad.OriginalValue
                : propiedad.CurrentValue;
        }

        return resultado;
    }

    private static bool PropiedadCambio(EntityEntry entry, string nombre) =>
        entry.Metadata.FindProperty(nombre) is not null
        && entry.Property(nombre).IsModified
        && !Equals(
            entry.Property(nombre).OriginalValue,
            entry.Property(nombre).CurrentValue);

    private static string ObtenerNombreEntidad(object entidad) => entidad switch
    {
        UsuarioAplicacion => "Usuario",
        UsuarioCarrera => "UsuarioCarrera",
        DocenteCarrera => "DocenteCarrera",
        IdentityUserRole<string> => "UsuarioRol",
        _ => entidad.GetType().Name
    };

    private static string ObtenerRegistroId(EntityEntry entry)
    {
        var llave = entry.Metadata.FindPrimaryKey();
        if (llave is null)
        {
            return string.Empty;
        }

        return string.Join(
            "|",
            llave.Properties.Select(propiedad =>
            {
                var valor = entry.Property(propiedad.Name).CurrentValue
                    ?? entry.Property(propiedad.Name).OriginalValue;
                return Convert.ToString(valor, CultureInfo.InvariantCulture)
                    ?? string.Empty;
            }));
    }

    private sealed record CambioPendiente(
        EntityEntry Entry,
        EntityState EstadoOriginal);
}

internal sealed class FechaHoraSistema : IFechaHora
{
    public DateTime UtcNow => DateTime.UtcNow;
}
