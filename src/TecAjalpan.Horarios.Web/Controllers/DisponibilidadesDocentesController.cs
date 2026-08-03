using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Docentes;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/docentes/{docenteId:guid}/disponibilidad")]
[Authorize(Roles = "Administrador,Secretaría,Jefatura,Subdirección Académica")]
public sealed class DisponibilidadesDocentesController(
    ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("{periodoId:guid}")]
    public async Task<ActionResult<DisponibilidadDocenteDto>> Obtener(
        Guid docenteId,
        Guid periodoId,
        CancellationToken cancellationToken)
    {
        var docente = await ObtenerDocenteAsync(docenteId, cancellationToken);
        if (docente is null)
        {
            return NotFound();
        }

        if (!await TieneAccesoAsync(docente, cancellationToken))
        {
            return Forbid();
        }

        var disponibilidad = await CargarDisponibilidadAsync(
            docenteId,
            periodoId,
            cancellationToken);

        return Ok(Mapear(disponibilidad, docente, periodoId));
    }

    [HttpPut("{periodoId:guid}")]
    [Authorize(Roles = "Administrador,Secretaría")]
    public async Task<ActionResult<DisponibilidadDocenteDto>> Guardar(
        Guid docenteId,
        Guid periodoId,
        GuardarDisponibilidadDocenteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PeriodoId != periodoId)
        {
            return BadRequest(new { mensaje = "El periodo de la solicitud no coincide." });
        }

        var docente = await ObtenerDocenteAsync(docenteId, cancellationToken);
        if (docente is null)
        {
            return NotFound();
        }

        if (!docente.Activo
            || !await PuedeCapturarAsync(docente, cancellationToken))
        {
            return Forbid();
        }

        var periodo = await dbContext.Periodos
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == periodoId, cancellationToken);
        if (periodo is null)
        {
            return BadRequest(new { mensaje = "El periodo no existe." });
        }
        if (periodo.Estado != EstadoPeriodo.Activo
            || periodo.FechaFin < DateOnly.FromDateTime(DateTime.Today))
        {
            return Conflict(new { mensaje = "La disponibilidad sólo puede modificarse en el periodo activo." });
        }

        var error = ValidarBorrador(docente.Tipo, request);
        if (error is not null)
        {
            return BadRequest(new { mensaje = error });
        }

        var disponibilidad = await dbContext.DisponibilidadesDocentes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.DocenteId == docenteId && x.PeriodoId == periodoId,
                cancellationToken);

        byte[]? versionEsperada = null;
        if (disponibilidad is not null)
        {
            versionEsperada = DecodificarRowVersion(request.RowVersion);
            if (versionEsperada is null
                || !versionEsperada.SequenceEqual(disponibilidad.RowVersion))
            {
                return Conflict(new
                {
                    mensaje = "La disponibilidad fue modificada por otra persona. Recarga e inténtalo nuevamente."
                });
            }
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Guid disponibilidadId;
            if (disponibilidad is null)
            {
                var nueva = new DisponibilidadDocente
                {
                    DocenteId = docenteId,
                    PeriodoId = periodoId,
                    Validada = false
                };
                dbContext.DisponibilidadesDocentes.Add(nueva);
                await dbContext.SaveChangesAsync(cancellationToken);
                disponibilidadId = nueva.Id;
            }
            else
            {
                var usuario = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.Identity?.Name
                    ?? "sistema";
                var fecha = DateTime.UtcNow;
                var filasActualizadas = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE [Recursos].[DisponibilidadesDocentes]
                    SET [Validada] = CAST(0 AS bit),
                        [FechaValidacion] = NULL,
                        [UsuarioValida] = NULL,
                        [FechaModifica] = {fecha},
                        [UsuarioModifica] = {usuario}
                    WHERE [Id] = {disponibilidad.Id}
                      AND [RowVersion] = {versionEsperada!}
                    """,
                    cancellationToken);
                if (filasActualizadas != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Conflict(new
                    {
                        mensaje = "La disponibilidad fue modificada por otra persona. Recarga e inténtalo nuevamente."
                    });
                }

                disponibilidadId = disponibilidad.Id;
            }

            await dbContext.JornadasDocentes
                .IgnoreQueryFilters()
                .Where(x => x.DisponibilidadDocenteId == disponibilidadId)
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.DisponibilidadesBloques
                .IgnoreQueryFilters()
                .Where(x => x.DisponibilidadDocenteId == disponibilidadId)
                .ExecuteDeleteAsync(cancellationToken);

            dbContext.JornadasDocentes.AddRange(
                request.Jornadas.Select(x => new JornadaDocente
                {
                    DisponibilidadDocenteId = disponibilidadId,
                    Dia = (DiaAcademico)x.Dia,
                    HoraInicio = x.HoraInicio,
                    HoraFin = x.HoraFin,
                    EsSemanaSabatina = false
                }));

            dbContext.DisponibilidadesBloques.AddRange(
                request.Bloques.Select(x => new DisponibilidadBloque
                {
                    DisponibilidadDocenteId = disponibilidadId,
                    Dia = (DiaAcademico)x.Dia,
                    Bloque = x.Bloque,
                    Disponible = true,
                    Preferente = x.Preferente
                }));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new
            {
                mensaje = "La disponibilidad fue modificada por otra persona. Recarga e inténtalo nuevamente."
            });
        }

        var guardada = await CargarDisponibilidadAsync(
            docenteId,
            periodoId,
            cancellationToken);
        return Ok(Mapear(guardada, docente, periodoId));
    }

    [HttpPost("{periodoId:guid}/validar")]
    [Authorize(Roles = "Administrador,Jefatura")]
    public async Task<ActionResult<DisponibilidadDocenteDto>> Validar(
        Guid docenteId,
        Guid periodoId,
        ValidarDisponibilidadRequest request,
        CancellationToken cancellationToken)
    {
        var docente = await ObtenerDocenteAsync(docenteId, cancellationToken);
        if (docente is null)
        {
            return NotFound();
        }
        if (!await TieneAccesoAsync(docente, cancellationToken))
        {
            return Forbid();
        }

        var disponibilidad = await dbContext.DisponibilidadesDocentes
            .Include(x => x.Jornadas)
            .Include(x => x.Bloques)
            .SingleOrDefaultAsync(
                x => x.DocenteId == docenteId && x.PeriodoId == periodoId,
                cancellationToken);
        if (disponibilidad is null)
        {
            return NotFound();
        }
        if (!CoincideRowVersion(request.RowVersion, disponibilidad.RowVersion))
        {
            return Conflict(new { mensaje = "La disponibilidad cambió. Recarga e inténtalo nuevamente." });
        }

        var error = ValidarParaConfirmar(docente.Tipo, disponibilidad);
        if (error is not null)
        {
            return BadRequest(new { mensaje = error });
        }

        disponibilidad.Validada = true;
        disponibilidad.FechaValidacion = DateTime.UtcNow;
        disponibilidad.UsuarioValida = User.Identity?.Name ?? "sistema";
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "La disponibilidad cambió mientras la validabas. Recarga e inténtalo nuevamente."
            });
        }

        return Ok(Mapear(disponibilidad, docente, periodoId));
    }

    private async Task<Docente?> ObtenerDocenteAsync(
        Guid docenteId,
        CancellationToken cancellationToken) =>
        await dbContext.Docentes
            .Include(x => x.Carreras)
            .SingleOrDefaultAsync(x => x.Id == docenteId, cancellationToken);

    private async Task<DisponibilidadDocente?> CargarDisponibilidadAsync(
        Guid docenteId,
        Guid periodoId,
        CancellationToken cancellationToken) =>
        await dbContext.DisponibilidadesDocentes
            .AsNoTracking()
            .Include(x => x.Jornadas)
            .Include(x => x.Bloques)
            .SingleOrDefaultAsync(
                x => x.DocenteId == docenteId && x.PeriodoId == periodoId,
                cancellationToken);

    private async Task<bool> TieneAccesoAsync(
        Docente docente,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(Roles.Administrador)
            || User.IsInRole(Roles.Subdireccion))
        {
            return true;
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (usuarioId is null)
        {
            return false;
        }

        var carrerasDocente = docente.Carreras
            .Select(x => x.CarreraId)
            .ToArray();
        return carrerasDocente.Length > 0
            && await dbContext.UsuariosCarreras
                .AsNoTracking()
                .AnyAsync(
                    x => x.UsuarioId == usuarioId
                        && carrerasDocente.Contains(x.CarreraId),
                    cancellationToken);
    }

    private async Task<bool> PuedeCapturarAsync(
        Docente docente,
        CancellationToken cancellationToken) =>
        User.IsInRole(Roles.Administrador)
        || User.IsInRole(Roles.Secretaria)
            && await TieneAccesoAsync(docente, cancellationToken);

    private static string? ValidarBorrador(
        TipoDocente tipo,
        GuardarDisponibilidadDocenteRequest request)
    {
        if (tipo == TipoDocente.Asignatura)
        {
            if (request.Jornadas.Count != 0)
            {
                return "Los docentes de asignatura sólo registran ventanas disponibles para clase.";
            }

            var bloques = request.Bloques
                .Select(x => (x.Dia, x.Bloque))
                .ToArray();
            if (bloques.Distinct().Count() != bloques.Length)
            {
                return "La disponibilidad debe contener bloques únicos.";
            }
            return bloques.Any(x => x.Dia is < 1 or > 6 || x.Bloque is < 1 or > 8)
                ? "Las clases sólo pueden registrarse en bloques de 08:00 a 16:00."
                : null;
        }

        if (request.Bloques.Count != 0)
        {
            return "A los docentes de tiempo completo sólo se les registra el inicio y fin de su jornada.";
        }
        if (request.Jornadas.Any(x => x.EsSemanaSabatina))
        {
            return "La jornada debe registrarse como una sola distribución semanal de lunes a sábado.";
        }

        return ValidarEstructuraJornada(request.Jornadas);
    }

    private static string? ValidarParaConfirmar(
        TipoDocente tipo,
        DisponibilidadDocente disponibilidad)
    {
        if (tipo == TipoDocente.Asignatura)
        {
            return disponibilidad.Bloques.Any(x => x.Disponible)
                ? null
                : "Selecciona al menos un bloque disponible antes de validar.";
        }

        var jornadas = disponibilidad.Jornadas
            .Where(x => !x.EsSemanaSabatina)
            .Select(x => new JornadaDocenteDto(
                (byte)x.Dia,
                x.HoraInicio,
                x.HoraFin,
                false))
            .ToArray();
        var error = ValidarEstructuraJornada(jornadas);
        if (error is not null)
        {
            return error;
        }

        return jornadas.Sum(x => (x.HoraFin - x.HoraInicio).TotalHours) == 40
            ? null
            : "La jornada de permanencia debe sumar exactamente 40 horas semanales antes de validar.";
    }

    private static string? ValidarEstructuraJornada(
        IReadOnlyCollection<JornadaDocenteDto> jornadas)
    {
        var dias = jornadas.Select(x => x.Dia).ToArray();
        if (dias.Distinct().Count() != dias.Length
            || dias.Any(x => x is < 1 or > 6))
        {
            return "La jornada semanal debe incluir días únicos entre lunes y sábado.";
        }

        var duraciones = jornadas
            .Select(x => new
            {
                x.HoraInicio,
                x.HoraFin,
                Duracion = x.HoraFin - x.HoraInicio
            })
            .ToArray();
        return duraciones.Any(x =>
            x.HoraInicio < new TimeOnly(7, 0)
            || x.HoraFin > new TimeOnly(18, 0)
            || x.Duracion <= TimeSpan.Zero
            || x.Duracion > TimeSpan.FromHours(8))
            ? "Cada día seleccionado debe tener más de 0 y hasta 8 horas, dentro del horario de 07:00 a 18:00."
            : null;
    }

    private static DisponibilidadDocenteDto Mapear(
        DisponibilidadDocente? disponibilidad,
        Docente docente,
        Guid periodoId) =>
        new(
            disponibilidad?.Id,
            periodoId,
            docente.Id,
            (byte)docente.Tipo,
            disponibilidad?.Validada ?? false,
            disponibilidad?.FechaValidacion,
            disponibilidad?.Jornadas
                .OrderBy(x => x.EsSemanaSabatina)
                .ThenBy(x => x.Dia)
                .Select(x => new JornadaDocenteDto(
                    (byte)x.Dia,
                    x.HoraInicio,
                    x.HoraFin,
                    x.EsSemanaSabatina))
                .ToArray() ?? [],
            disponibilidad?.Bloques
                .Where(x => x.Disponible)
                .OrderBy(x => x.Dia)
                .ThenBy(x => x.Bloque)
                .Select(x => new DisponibilidadBloqueDto(
                    (byte)x.Dia,
                    x.Bloque,
                    x.Preferente))
                .ToArray() ?? [],
            disponibilidad is null
                ? null
                : Convert.ToBase64String(disponibilidad.RowVersion));

    private static byte[]? DecodificarRowVersion(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }
        try
        {
            return Convert.FromBase64String(valor);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool CoincideRowVersion(string? valor, byte[] actual) =>
        DecodificarRowVersion(valor)?.SequenceEqual(actual) == true;
}
