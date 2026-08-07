using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Espacios;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/espacios")]
[Authorize(Roles = "Administrador,Jefatura,Subdirección Académica")]
public sealed class EspaciosController(
    ApplicationDbContext dbContext,
    IUsuarioActual usuarioActual) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EspaciosCatalogoDto>> Listar(
        CancellationToken cancellationToken)
    {
        var alcanceInstitucional = TieneAlcanceInstitucional();
        var idsCarrerasAdministrables = alcanceInstitucional
            ? []
            : ObtenerCarrerasAsignadas();
        var carreras = dbContext.Carreras.AsNoTracking();
        var espacios = dbContext.Espacios
            .AsNoTracking()
            .Include(x => x.Carrera)
            .Include(x => x.CarrerasCompartidas)
            .AsQueryable();

        if (!alcanceInstitucional)
        {
            espacios = espacios.Where(x => idsCarrerasAdministrables.Contains(x.CarreraId));
        }

        var carrerasDto = await carreras
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.Nombre)
            .Select(x => new EspacioCarreraDto(
                x.Id,
                x.Clave,
                x.Nombre,
                x.Activo,
                alcanceInstitucional || idsCarrerasAdministrables.Contains(x.Id)))
            .ToArrayAsync(cancellationToken);

        var espaciosEntidades = await espacios
            .OrderBy(x => x.Carrera.Nombre)
            .ThenByDescending(x => x.Activo)
            .ThenBy(x => x.Nombre)
            .ToArrayAsync(cancellationToken);
        var espaciosDto = espaciosEntidades
            .Select(Mapear)
            .ToArray();

        return Ok(new EspaciosCatalogoDto(carrerasDto, espaciosDto));
    }

    [HttpPost]
    public async Task<ActionResult<EspacioDto>> Crear(
        GuardarEspacioRequest request,
        CancellationToken cancellationToken)
    {
        if (!usuarioActual.PuedeAccederCarrera(request.CarreraId))
        {
            return Forbid();
        }

        var validacion = await ValidarRequest(
            request,
            espacioId: null,
            carreraActualId: null,
            cancellationToken);
        if (validacion is not null)
        {
            return Conflict(new { mensaje = validacion });
        }

        var espacio = new Espacio();
        Aplicar(request, espacio);
        AgregarCarrerasCompartidas(request, espacio);
        dbContext.Espacios.Add(espacio);

        var resultado = await Guardar(espacio, creado: true, cancellationToken);
        return resultado;
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EspacioDto>> Actualizar(
        Guid id,
        GuardarEspacioRequest request,
        CancellationToken cancellationToken)
    {
        var espacio = await dbContext.Espacios
            .Include(x => x.Carrera)
            .Include(x => x.CarrerasCompartidas)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (espacio is null)
        {
            return NotFound();
        }

        if (!usuarioActual.PuedeAccederCarrera(espacio.CarreraId)
            || !usuarioActual.PuedeAccederCarrera(request.CarreraId))
        {
            return Forbid();
        }

        if (espacio.CarreraId != request.CarreraId
            && await dbContext.Grupos.AnyAsync(
                x => x.EspacioBaseId == espacio.Id,
                cancellationToken))
        {
            return Conflict(new
            {
                mensaje = "No se puede cambiar la carrera porque el espacio ya está asignado a uno o más grupos."
            });
        }

        var validacion = await ValidarRequest(
            request,
            id,
            espacio.CarreraId,
            cancellationToken);
        if (validacion is not null)
        {
            return Conflict(new { mensaje = validacion });
        }

        var idsCompartidasNuevas = request.CarreraIdsCompartidas
            .Distinct()
            .ToArray();
        var idsRetiradas = espacio.CarrerasCompartidas
            .Where(x => !x.Eliminado)
            .Select(x => x.CarreraId)
            .Except(idsCompartidasNuevas)
            .ToArray();
        if (idsRetiradas.Length > 0)
        {
            var permisoEnUso = await dbContext.Grupos.AnyAsync(x =>
                x.EspacioBaseId == espacio.Id
                && idsRetiradas.Contains(x.PeriodoCarrera.CarreraId)
                && x.PeriodoCarrera.Periodo.Estado != EstadoPeriodo.Cerrado,
                cancellationToken);
            if (permisoEnUso)
            {
                return Conflict(new
                {
                    mensaje = "No se puede dejar de compartir el espacio con una carrera que lo tiene asignado a un grupo de una oferta vigente. Cambia primero el espacio del grupo."
                });
            }
        }

        Aplicar(request, espacio);
        SincronizarCarrerasCompartidas(idsCompartidasNuevas, espacio);

        // El Espacio es la raíz del agregado. Aun cuando sólo cambien las
        // carreras compartidas, hacemos que avance su RowVersion. EF conserva
        // como OriginalValue la versión que acaba de leer de SQL y la valida
        // de forma atómica al ejecutar el UPDATE.
        dbContext.Entry(espacio)
            .Property(x => x.FechaModifica)
            .IsModified = true;

        return await Guardar(espacio, creado: false, cancellationToken);
    }

    [HttpPatch("{id:guid}/estado")]
    public async Task<ActionResult<EspacioDto>> CambiarEstado(
        Guid id,
        CambiarEstadoEspacioRequest request,
        CancellationToken cancellationToken)
    {
        var espacio = await dbContext.Espacios
            .Include(x => x.Carrera)
            .Include(x => x.CarrerasCompartidas)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (espacio is null)
        {
            return NotFound();
        }

        if (!usuarioActual.PuedeAccederCarrera(espacio.CarreraId))
        {
            return Forbid();
        }

        if (espacio.Activo == request.Activo)
        {
            return Ok(Mapear(espacio));
        }

        if (request.Activo && !espacio.Carrera.Activo)
        {
            return Conflict(new
            {
                mensaje = "No se puede activar el espacio porque su carrera está inactiva."
            });
        }

        if (!request.Activo)
        {
            var asignadoEnOfertaVigente = await dbContext.Grupos.AnyAsync(
                x => x.EspacioBaseId == espacio.Id
                    && x.PeriodoCarrera.Periodo.Estado != EstadoPeriodo.Cerrado,
                cancellationToken);
            if (asignadoEnOfertaVigente)
            {
                return Conflict(new
                {
                    mensaje = "No se puede desactivar porque el espacio está asignado a un grupo de una oferta vigente. Cambia primero el espacio del grupo."
                });
            }
        }

        espacio.Activo = request.Activo;
        return await Guardar(espacio, creado: false, cancellationToken);
    }

    private async Task<string?> ValidarRequest(
        GuardarEspacioRequest request,
        Guid? espacioId,
        Guid? carreraActualId,
        CancellationToken cancellationToken)
    {
        var carreraActiva = await dbContext.Carreras
            .Where(x => x.Id == request.CarreraId)
            .Select(x => (bool?)x.Activo)
            .SingleOrDefaultAsync(cancellationToken);
        if (!carreraActiva.HasValue)
        {
            return "La carrera seleccionada no existe.";
        }

        if (!carreraActiva.Value && carreraActualId != request.CarreraId)
        {
            return "No se pueden registrar espacios nuevos en una carrera inactiva.";
        }

        var clave = request.Clave.Trim().ToUpperInvariant();
        var claveDuplicada = await dbContext.Espacios.AnyAsync(
            x => x.Clave == clave
                && (!espacioId.HasValue || x.Id != espacioId.Value),
            cancellationToken);
        if (claveDuplicada)
        {
            return "Ya existe un aula o laboratorio con esa clave.";
        }


        var idsCompartidas = request.CarreraIdsCompartidas
            .Distinct()
            .ToArray();
        if (idsCompartidas.Contains(request.CarreraId))
        {
            return "La carrera responsable ya tiene acceso al espacio y no debe agregarse como compartida.";
        }

        if (idsCompartidas.Length > 0)
        {
            var carrerasCompartidasValidas = await dbContext.Carreras.CountAsync(
                x => idsCompartidas.Contains(x.Id) && x.Activo,
                cancellationToken);
            if (carrerasCompartidasValidas != idsCompartidas.Length)
            {
                return "Todas las carreras seleccionadas para compartir el espacio deben existir y estar activas.";
            }
        }

        return null;
    }

    private async Task<ActionResult<EspacioDto>> Guardar(
        Espacio espacio,
        bool creado,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflicto();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                mensaje = "Ya existe un aula o laboratorio con esa clave."
            });
        }

        var carrera = await dbContext.Carreras
            .AsNoTracking()
            .Where(x => x.Id == espacio.CarreraId)
            .Select(x => new { x.Clave, x.Nombre })
            .SingleAsync(cancellationToken);

        var dto = Mapear(espacio, carrera.Clave, carrera.Nombre);
        return creado
            ? CreatedAtAction(nameof(Listar), dto)
            : Ok(dto);
    }

    private bool TieneAlcanceInstitucional() =>
        User.IsInRole(Roles.Administrador)
        || User.IsInRole(Roles.Subdireccion);

    private Guid[] ObtenerCarrerasAsignadas() =>
        User.FindAll("carrera")
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

    private static void Aplicar(
        GuardarEspacioRequest request,
        Espacio espacio)
    {
        espacio.CarreraId = request.CarreraId;
        espacio.Clave = request.Clave.Trim().ToUpperInvariant();
        espacio.Nombre = request.Nombre.Trim();
        espacio.Tipo = request.Tipo == "Laboratorio"
            ? "Laboratorio"
            : "Aula";
        espacio.Capacidad = request.Capacidad;
        espacio.Especialidad = string.IsNullOrWhiteSpace(request.Especialidad)
            ? null
            : request.Especialidad.Trim();
    }

    private static void AgregarCarrerasCompartidas(
        GuardarEspacioRequest request,
        Espacio espacio)
    {
        foreach (var carreraId in request.CarreraIdsCompartidas.Distinct())
        {
            espacio.CarrerasCompartidas.Add(new EspacioCarreraCompartida
            {
                CarreraId = carreraId
            });
        }
    }

    private void SincronizarCarrerasCompartidas(
        Guid[] carreraIds,
        Espacio espacio)
    {
        var actuales = espacio.CarrerasCompartidas
            .Where(x => !x.Eliminado)
            .ToArray();

        foreach (var relacion in actuales.Where(x => !carreraIds.Contains(x.CarreraId)))
        {
            dbContext.EspaciosCarrerasCompartidas.Remove(relacion);
        }

        var actualesIds = actuales.Select(x => x.CarreraId).ToHashSet();
        foreach (var carreraId in carreraIds.Where(x => !actualesIds.Contains(x)))
        {
            dbContext.EspaciosCarrerasCompartidas.Add(new EspacioCarreraCompartida
            {
                EspacioId = espacio.Id,
                CarreraId = carreraId
            });
        }
    }

    private static EspacioDto Mapear(Espacio espacio) =>
        Mapear(espacio, espacio.Carrera.Clave, espacio.Carrera.Nombre);

    private static EspacioDto Mapear(
        Espacio espacio,
        string carreraClave,
        string carreraNombre) =>
        new(
            espacio.Id,
            espacio.CarreraId,
            carreraClave,
            carreraNombre,
            espacio.Clave,
            espacio.Nombre,
            espacio.Tipo,
            espacio.Capacidad,
            espacio.Especialidad,
            espacio.CarrerasCompartidas
                .Where(x => !x.Eliminado)
                .Select(x => x.CarreraId)
                .OrderBy(x => x)
                .ToArray(),
            espacio.Activo,
            Convert.ToBase64String(espacio.RowVersion));

    private ConflictObjectResult Conflicto() =>
        Conflict(new
        {
            mensaje = "El espacio fue modificado por otra persona. Recarga los datos e inténtalo nuevamente."
        });


}
