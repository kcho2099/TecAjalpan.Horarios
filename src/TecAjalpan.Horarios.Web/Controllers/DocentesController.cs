using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Carreras;
using TecAjalpan.Horarios.Contracts.Docentes;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/docentes")]
[Authorize(Roles = "Administrador,Secretaría,Jefatura,Subdirección Académica")]
public sealed class DocentesController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocenteDto>>> Listar(
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Docentes
            .AsNoTracking()
            .Include(x => x.Carreras)
            .ThenInclude(x => x.Carrera)
            .AsQueryable();

        if (!TieneAlcanceInstitucional())
        {
            var carreras = ObtenerCarrerasUsuario();
            consulta = consulta.Where(x =>
                x.Carreras.Any(c => carreras.Contains(c.CarreraId)));
        }

        if (!User.IsInRole(Roles.Administrador)
            && !User.IsInRole(Roles.Secretaria))
        {
            consulta = consulta.Where(x => x.Activo);
        }

        var docentes = await consulta
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.Apellidos)
            .ThenBy(x => x.Nombres)
            .ToArrayAsync(cancellationToken);

        return Ok(docentes.Select(Mapear).ToArray());
    }

    [HttpGet("carreras-disponibles")]
    public async Task<ActionResult<IReadOnlyList<CarreraDto>>> ListarCarrerasDisponibles(
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Carreras
            .AsNoTracking()
            .Where(x => x.Activo);

        if (!TieneAlcanceInstitucional())
        {
            var carreras = ObtenerCarrerasUsuario();
            consulta = consulta.Where(x => carreras.Contains(x.Id));
        }

        var carrerasDisponibles = await consulta
            .OrderBy(x => x.Nombre)
            .ToArrayAsync(cancellationToken);
        var resultado = carrerasDisponibles
            .Select(x => new CarreraDto(
                x.Id,
                x.Clave,
                x.Nombre,
                x.Activo,
                Convert.ToBase64String(x.RowVersion)))
            .ToArray();
        return Ok(resultado);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Secretaría")]
    public async Task<ActionResult<DocenteDto>> Crear(
        GuardarDocenteRequest request,
        CancellationToken cancellationToken)
    {
        var error = await ValidarAsync(request, null, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { mensaje = error });
        }

        var docente = new Docente();
        Aplicar(request, docente);
        docente.Carreras = request.CarreraIds
            .Distinct()
            .Select(id => new DocenteCarrera { CarreraId = id })
            .ToList();
        dbContext.Docentes.Add(docente);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                mensaje = "Ya existe un docente con ese número de trabajador o correo."
            });
        }

        await CargarCarrerasAsync(docente, cancellationToken);
        return CreatedAtAction(nameof(Listar), Mapear(docente));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrador,Secretaría")]
    public async Task<ActionResult<DocenteDto>> Actualizar(
        Guid id,
        GuardarDocenteRequest request,
        CancellationToken cancellationToken)
    {
        var docente = await dbContext.Docentes
            .Include(x => x.Carreras)
            .ThenInclude(x => x.Carrera)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (docente is null)
        {
            return NotFound();
        }

        if (!docente.Activo)
        {
            return Conflict(new
            {
                mensaje = "Primero activa al docente para poder editar sus datos."
            });
        }

        if (!PuedeAdministrar(docente.Carreras.Select(x => x.CarreraId)))
        {
            return Forbid();
        }

        if (!CoincideRowVersion(request.RowVersion, docente.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "El docente fue modificado por otra persona. Recarga los datos e inténtalo nuevamente."
            });
        }

        var error = await ValidarAsync(request, id, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { mensaje = error });
        }

        Aplicar(request, docente);
        SincronizarCarreras(docente, request.CarreraIds);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "El docente cambió mientras lo editabas. Recarga los datos e inténtalo nuevamente."
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                mensaje = "Ya existe un docente con ese número de trabajador o correo."
            });
        }

        await CargarCarrerasAsync(docente, cancellationToken);
        return Ok(Mapear(docente));
    }

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = "Administrador,Secretaría")]
    public async Task<ActionResult<DocenteDto>> CambiarEstado(
        Guid id,
        CambiarEstadoDocenteRequest request,
        CancellationToken cancellationToken)
    {
        var docente = await dbContext.Docentes
            .Include(x => x.Carreras)
            .ThenInclude(x => x.Carrera)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (docente is null)
        {
            return NotFound();
        }

        if (!PuedeAdministrar(docente.Carreras.Select(x => x.CarreraId)))
        {
            return Forbid();
        }

        if (!CoincideRowVersion(request.RowVersion, docente.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "El docente fue modificado por otra persona. Recarga los datos e inténtalo nuevamente."
            });
        }

        docente.Activo = request.Activo;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "El docente cambió mientras actualizabas su estado. Recarga los datos e inténtalo nuevamente."
            });
        }

        return Ok(Mapear(docente));
    }

    private async Task<string?> ValidarAsync(
        GuardarDocenteRequest request,
        Guid? docenteId,
        CancellationToken cancellationToken)
    {
        var carreraIds = request.CarreraIds.Distinct().ToArray();
        if (carreraIds.Length == 0)
        {
            return "Asigna al menos una carrera al docente.";
        }

        if (request.CargaMaximaSemanal > request.HorasPermanenciaSemanal)
        {
            return "La carga máxima no puede superar las horas de permanencia.";
        }

        if (!PuedeAdministrar(carreraIds))
        {
            return "No puedes asignar carreras fuera de tu alcance.";
        }

        var activas = await dbContext.Carreras
            .CountAsync(x => carreraIds.Contains(x.Id) && x.Activo, cancellationToken);
        if (activas != carreraIds.Length)
        {
            return "Selecciona únicamente carreras activas y existentes.";
        }

        var numero = request.NumeroTrabajador.Trim().ToUpperInvariant();
        var correo = request.Correo.Trim().ToUpperInvariant();
        var duplicado = await dbContext.Docentes.AnyAsync(
            x => x.Id != docenteId
                && (x.NumeroTrabajador.ToUpper() == numero
                    || x.Correo.ToUpper() == correo),
            cancellationToken);
        return duplicado
            ? "Ya existe un docente con ese número de trabajador o correo."
            : null;
    }

    private bool TieneAlcanceInstitucional() =>
        User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Subdireccion);

    private bool PuedeAdministrar(IEnumerable<Guid> carreraIds)
    {
        if (User.IsInRole(Roles.Administrador))
        {
            return true;
        }

        var permitidas = ObtenerCarrerasUsuario();
        var solicitadas = carreraIds.Distinct().ToArray();
        return solicitadas.Length > 0 && solicitadas.All(permitidas.Contains);
    }

    private HashSet<Guid> ObtenerCarrerasUsuario() =>
        User.FindAll("carrera")
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToHashSet();

    private static void Aplicar(GuardarDocenteRequest request, Docente docente)
    {
        docente.NumeroTrabajador = request.NumeroTrabajador.Trim().ToUpperInvariant();
        docente.Nombres = request.Nombres.Trim();
        docente.Apellidos = request.Apellidos.Trim();
        docente.Correo = request.Correo.Trim().ToLowerInvariant();
        docente.Tipo = (TipoDocente)request.Tipo;
        docente.HorasPermanenciaSemanal = checked((byte)request.HorasPermanenciaSemanal);
        docente.CargaMaximaSemanal = checked((byte)request.CargaMaximaSemanal);
    }

    private void SincronizarCarreras(Docente docente, IEnumerable<Guid> carreraIds)
    {
        var solicitadas = carreraIds.Distinct().ToHashSet();
        foreach (var relacion in docente.Carreras
                     .Where(x => !solicitadas.Contains(x.CarreraId))
                     .ToArray())
        {
            dbContext.DocentesCarreras.Remove(relacion);
        }

        var actuales = docente.Carreras.Select(x => x.CarreraId).ToHashSet();
        foreach (var carreraId in solicitadas.Where(x => !actuales.Contains(x)))
        {
            docente.Carreras.Add(new DocenteCarrera { CarreraId = carreraId });
        }
    }

    private async Task CargarCarrerasAsync(
        Docente docente,
        CancellationToken cancellationToken)
    {
        await dbContext.Entry(docente)
            .Collection(x => x.Carreras)
            .Query()
            .Include(x => x.Carrera)
            .LoadAsync(cancellationToken);
    }

    private static DocenteDto Mapear(Docente docente) =>
        new(
            docente.Id,
            docente.NumeroTrabajador,
            docente.Nombres,
            docente.Apellidos,
            docente.Correo,
            (byte)docente.Tipo,
            docente.Tipo == TipoDocente.TiempoCompleto
                ? "Tiempo completo"
                : "Asignatura",
            docente.HorasPermanenciaSemanal,
            docente.CargaMaximaSemanal,
            docente.Activo,
            docente.Carreras
                .Where(x => !x.Eliminado)
                .OrderBy(x => x.Carrera.Nombre)
                .Select(x => new DocenteCarreraDto(
                    x.CarreraId,
                    x.Carrera.Clave,
                    x.Carrera.Nombre))
                .ToArray(),
            Convert.ToBase64String(docente.RowVersion));

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
