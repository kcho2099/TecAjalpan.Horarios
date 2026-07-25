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

        if (!TieneAcceso(docente))
        {
            return Forbid();
        }

        var disponibilidad = await dbContext.DisponibilidadesDocentes
            .AsNoTracking()
            .Include(x => x.Jornadas)
            .Include(x => x.Bloques)
            .SingleOrDefaultAsync(
                x => x.DocenteId == docenteId && x.PeriodoId == periodoId,
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

        if (!docente.Activo || !PuedeCapturar(docente))
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
        if (periodo.Estado == EstadoPeriodo.Cerrado)
        {
            return Conflict(new { mensaje = "No se puede modificar la disponibilidad de un periodo cerrado." });
        }

        var error = ValidarReglas(docente.Tipo, request);
        if (error is not null)
        {
            return BadRequest(new { mensaje = error });
        }

        var disponibilidad = await dbContext.DisponibilidadesDocentes
            .Include(x => x.Jornadas)
            .Include(x => x.Bloques)
            .SingleOrDefaultAsync(
                x => x.DocenteId == docenteId && x.PeriodoId == periodoId,
                cancellationToken);

        if (disponibilidad is null)
        {
            disponibilidad = new DisponibilidadDocente
            {
                DocenteId = docenteId,
                PeriodoId = periodoId
            };
            dbContext.DisponibilidadesDocentes.Add(disponibilidad);
        }
        else if (!CoincideRowVersion(request.RowVersion, disponibilidad.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "La disponibilidad fue modificada por otra persona. Recarga e inténtalo nuevamente."
            });
        }

        SincronizarJornadas(disponibilidad, request.Jornadas);
        SincronizarBloques(disponibilidad, request.Bloques);
        disponibilidad.Validada = false;
        disponibilidad.FechaValidacion = null;
        disponibilidad.UsuarioValida = null;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "La disponibilidad cambió mientras la editabas. Recarga e inténtalo nuevamente."
            });
        }

        return Ok(Mapear(disponibilidad, docente, periodoId));
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
        if (!TieneAcceso(docente))
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

        disponibilidad.Validada = true;
        disponibilidad.FechaValidacion = DateTime.UtcNow;
        disponibilidad.UsuarioValida = User.Identity?.Name ?? "sistema";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Mapear(disponibilidad, docente, periodoId));
    }

    private async Task<Docente?> ObtenerDocenteAsync(
        Guid docenteId,
        CancellationToken cancellationToken) =>
        await dbContext.Docentes
            .Include(x => x.Carreras)
            .SingleOrDefaultAsync(x => x.Id == docenteId, cancellationToken);

    private bool TieneAcceso(Docente docente) =>
        User.IsInRole(Roles.Administrador)
        || User.IsInRole(Roles.Subdireccion)
        || docente.Carreras.Any(x =>
            x.EsPrincipal && ObtenerCarrerasUsuario().Contains(x.CarreraId));

    private bool PuedeCapturar(Docente docente) =>
        User.IsInRole(Roles.Administrador)
        || User.IsInRole(Roles.Secretaria) && TieneAcceso(docente);

    private HashSet<Guid> ObtenerCarrerasUsuario() =>
        User.FindAll("carrera")
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToHashSet();

    private static string? ValidarReglas(
        TipoDocente tipo,
        GuardarDisponibilidadDocenteRequest request)
    {
        var bloques = request.Bloques
            .Select(x => (x.Dia, x.Bloque))
            .ToArray();
        if (bloques.Length == 0 || bloques.Distinct().Count() != bloques.Length)
        {
            return "La disponibilidad debe contener bloques únicos.";
        }
        if (bloques.Any(x => x.Dia is < 1 or > 6 || x.Bloque is < 1 or > 8))
        {
            return "Las clases sólo pueden registrarse en bloques de 08:00 a 16:00.";
        }

        if (tipo == TipoDocente.Asignatura)
        {
            return request.Jornadas.Count == 0
                ? null
                : "Los docentes de asignatura sólo registran ventanas disponibles para clase.";
        }

        var jornadas = request.Jornadas.ToArray();
        if (jornadas.Length != 5
            || jornadas.Select(x => x.Dia).Distinct().Count() != 5
            || jornadas.Any(x => x.Dia is < 1 or > 5))
        {
            return "Tiempo completo requiere una jornada de lunes a viernes.";
        }
        if (jornadas.Any(x =>
                x.HoraInicio < new TimeOnly(7, 0)
                || x.HoraFin > new TimeOnly(18, 0)
                || x.HoraFin - x.HoraInicio != TimeSpan.FromHours(8)))
        {
            return "Cada jornada debe cubrir exactamente 8 horas entre 07:00 y 18:00.";
        }

        foreach (var bloque in request.Bloques)
        {
            var jornada = jornadas.SingleOrDefault(x => x.Dia == bloque.Dia);
            var inicioBloque = new TimeOnly(8 + bloque.Bloque - 1, 0);
            if (jornada is null
                || inicioBloque < jornada.HoraInicio
                || inicioBloque.AddHours(1) > jornada.HoraFin)
            {
                return "Todos los bloques para clase deben quedar dentro de la jornada diaria.";
            }
        }

        return null;
    }

    private void SincronizarJornadas(
        DisponibilidadDocente disponibilidad,
        IEnumerable<JornadaDocenteDto> solicitadas)
    {
        var nuevas = solicitadas.ToDictionary(x => x.Dia);
        foreach (var actual in disponibilidad.Jornadas.ToArray())
        {
            if (!nuevas.Remove((byte)actual.Dia, out var solicitada))
            {
                dbContext.JornadasDocentes.Remove(actual);
                continue;
            }
            actual.HoraInicio = solicitada.HoraInicio;
            actual.HoraFin = solicitada.HoraFin;
        }

        foreach (var nueva in nuevas.Values
            .Select(x => new JornadaDocente
            {
                Dia = (DiaAcademico)x.Dia,
                HoraInicio = x.HoraInicio,
                HoraFin = x.HoraFin
            }))
        {
            disponibilidad.Jornadas.Add(nueva);
        }
    }

    private void SincronizarBloques(
        DisponibilidadDocente disponibilidad,
        IEnumerable<DisponibilidadBloqueDto> solicitados)
    {
        var nuevos = solicitados.ToDictionary(x => (x.Dia, x.Bloque));
        foreach (var actual in disponibilidad.Bloques.ToArray())
        {
            if (!nuevos.Remove(((byte)actual.Dia, actual.Bloque), out var solicitado))
            {
                dbContext.DisponibilidadesBloques.Remove(actual);
                continue;
            }
            actual.Disponible = true;
            actual.Preferente = solicitado.Preferente;
        }

        foreach (var nuevo in nuevos.Values
            .Select(x => new DisponibilidadBloque
            {
                Dia = (DiaAcademico)x.Dia,
                Bloque = x.Bloque,
                Disponible = true,
                Preferente = x.Preferente
            }))
        {
            disponibilidad.Bloques.Add(nuevo);
        }
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
                .OrderBy(x => x.Dia)
                .Select(x => new JornadaDocenteDto(
                    (byte)x.Dia,
                    x.HoraInicio,
                    x.HoraFin))
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

    private static bool CoincideRowVersion(string? valor, byte[] actual)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }
        try
        {
            return Convert.FromBase64String(valor).SequenceEqual(actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
