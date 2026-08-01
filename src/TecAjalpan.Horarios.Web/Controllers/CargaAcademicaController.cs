using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.CargaAcademica;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/carga-academica")]
[Authorize(Roles = Roles.Administrador + "," + Roles.Jefatura + "," + Roles.Subdireccion)]
public sealed class CargaAcademicaController(
    ApplicationDbContext dbContext,
    IUsuarioActual usuarioActual) : ControllerBase
{
    [HttpGet("catalogos")]
    public async Task<ActionResult<CargaAcademicaCatalogosDto>> Catalogos(
        CancellationToken cancellationToken)
    {
        var periodos = await dbContext.Periodos.AsNoTracking()
            .Where(x => x.Estado != EstadoPeriodo.Cerrado)
            .OrderByDescending(x => x.FechaInicio)
            .Select(x => new CargaPeriodoDto(x.Id, x.Nombre, (byte)x.Estado))
            .ToArrayAsync(cancellationToken);

        var carrerasConsulta = dbContext.Carreras.AsNoTracking().Where(x => x.Activo);
        if (!TieneAlcanceInstitucional())
        {
            var carrerasUsuario = CarrerasUsuario();
            carrerasConsulta = carrerasConsulta.Where(x => carrerasUsuario.Contains(x.Id));
        }

        var carreras = await carrerasConsulta
            .OrderBy(x => x.Nombre)
            .Select(x => new CargaCarreraDto(x.Id, x.Clave, x.Nombre))
            .ToArrayAsync(cancellationToken);

        var modalidades = await dbContext.Modalidades.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Tipo)
            .Select(x => new CargaModalidadDto(x.Id, x.Clave, x.Nombre))
            .ToArrayAsync(cancellationToken);

        return Ok(new CargaAcademicaCatalogosDto(
            periodos, carreras, modalidades));
    }

    [HttpGet]
    public async Task<ActionResult<CargaConfiguracionDto>> Obtener(
        [FromQuery] Guid periodoId,
        [FromQuery] Guid carreraId,
        [FromQuery] Guid modalidadId,
        CancellationToken cancellationToken)
    {
        if (!usuarioActual.PuedeAccederCarrera(carreraId))
            return Forbid();

        var configuracion = await dbContext.PeriodosCarreras.AsNoTracking()
            .Include(x => x.Carrera)
            .Include(x => x.Modalidad)
            .Include(x => x.Grupos).ThenInclude(x => x.EspacioBase)
            .Include(x => x.Grupos).ThenInclude(x => x.Oferta).ThenInclude(x => x.Materia)
            .SingleOrDefaultAsync(x =>
                x.PeriodoId == periodoId
                && x.CarreraId == carreraId
                && x.ModalidadId == modalidadId,
                cancellationToken);
        if (configuracion is null)
        {
            return NotFound(new
            {
                mensaje = "No existe oferta académica para la selección indicada."
            });
        }

        var ofertasIds = configuracion.Grupos
            .SelectMany(x => x.Oferta)
            .Where(x => x.Activa)
            .Select(x => x.Id)
            .ToArray();
        var asignaciones = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => ofertasIds.Contains(x.OfertaMateriaId))
            .Include(x => x.Docente)
            .ToArrayAsync(cancellationToken);

        var docentesCarrera = await dbContext.DocentesCarreras.AsNoTracking()
            .Where(x => x.CarreraId == carreraId
                && x.Docente.Activo
                && dbContext.DisponibilidadesDocentes.Any(disponibilidad =>
                    disponibilidad.DocenteId == x.DocenteId
                    && disponibilidad.PeriodoId == periodoId
                    && disponibilidad.Validada))
            .Select(x => new
            {
                x.DocenteId,
                x.Docente.Apellidos,
                x.Docente.Nombres,
                x.Docente.Tipo,
                x.Docente.CargaMaximaSemanal
            })
            .ToArrayAsync(cancellationToken);
        var docentesCarreraIds = docentesCarrera
            .Select(x => x.DocenteId)
            .ToArray();

        var cargasDocentes = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => docentesCarreraIds.Contains(x.DocenteId)
                && x.OfertaMateria.Activa
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == periodoId)
            .Select(x => new
            {
                x.DocenteId,
                HorasAsignadas = (int)x.OfertaMateria.HorasRequeridas
            })
            .GroupBy(x => x.DocenteId)
            .Select(x => new
            {
                DocenteId = x.Key,
                HorasAsignadas = x.Sum(c => c.HorasAsignadas)
            })
            .ToArrayAsync(cancellationToken);

        var disponibilidadesValidadas = await dbContext.DisponibilidadesDocentes
            .AsNoTracking()
            .Where(x => docentesCarreraIds.Contains(x.DocenteId)
                && x.PeriodoId == periodoId
                && x.Validada)
            .Select(x => new
            {
                x.DocenteId,
                HorasDisponibles = x.Bloques.Count(b => b.Disponible)
            })
            .ToArrayAsync(cancellationToken);

        var resumenDocentes = docentesCarrera
            .Select(docente =>
            {
                var disponibilidad = disponibilidadesValidadas
                    .SingleOrDefault(x => x.DocenteId == docente.DocenteId);
                return new CargaDocenteResumenDto(
                    docente.DocenteId,
                    docente.Apellidos + ", " + docente.Nombres,
                    (byte)docente.Tipo,
                    cargasDocentes
                        .SingleOrDefault(x => x.DocenteId == docente.DocenteId)
                        ?.HorasAsignadas ?? 0,
                    docente.CargaMaximaSemanal,
                    docente.Tipo == TipoDocente.Asignatura
                        ? disponibilidad?.HorasDisponibles
                        : null);
            })
            .OrderByDescending(x => x.HorasAsignadas)
            .ThenBy(x => x.DocenteNombre)
            .ToArray();

        return Ok(Mapear(configuracion, asignaciones, resumenDocentes));
    }

    [HttpPut("materias/{ofertaMateriaId:guid}")]
    [Authorize(Roles = Roles.Administrador + "," + Roles.Jefatura)]
    public async Task<ActionResult<CargaMateriaDto>> Guardar(
        Guid ofertaMateriaId,
        GuardarCargaAcademicaRequest request,
        CancellationToken cancellationToken)
    {
        if (ofertaMateriaId != request.OfertaMateriaId)
        {
            return BadRequest(new
            {
                mensaje = "La materia enviada no coincide con la ruta."
            });
        }
        if (request.DocenteId == Guid.Empty)
            return BadRequest(new { mensaje = "Selecciona al docente titular." });

        var oferta = await dbContext.OfertasMaterias
            .Include(x => x.Materia)
            .Include(x => x.Grupo)
                .ThenInclude(x => x.PeriodoCarrera)
                .ThenInclude(x => x.Periodo)
            .SingleOrDefaultAsync(
                x => x.Id == ofertaMateriaId && x.Activa,
                cancellationToken);
        if (oferta is null)
        {
            return NotFound(new
            {
                mensaje = "La materia ya no forma parte de la oferta académica."
            });
        }
        if (!usuarioActual.PuedeAccederCarrera(
                oferta.Grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (oferta.Grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
        {
            return Conflict(new
            {
                mensaje = "No se puede modificar la carga de un periodo cerrado."
            });
        }

        var errorDocente = await ValidarDocente(
            oferta, request.DocenteId, cancellationToken);
        if (errorDocente is not null)
            return BadRequest(new { mensaje = errorDocente });

        var asignacion = await dbContext.CargasAcademicas
            .SingleOrDefaultAsync(
                x => x.OfertaMateriaId == ofertaMateriaId,
                cancellationToken);
        if (!VersionCoincide(asignacion, request.RowVersion))
            return Conflicto();

        if (asignacion is null)
        {
            asignacion = new CargaAcademica
            {
                OfertaMateriaId = ofertaMateriaId
            };
            dbContext.CargasAcademicas.Add(asignacion);
        }

        asignacion.DocenteId = request.DocenteId;
        asignacion.Observaciones = string.IsNullOrWhiteSpace(request.Observaciones)
            ? null
            : request.Observaciones.Trim();
        asignacion.Estado = EstadoCarga.Borrador;
        asignacion.FechaAutorizacion = null;
        asignacion.UsuarioAutoriza = null;

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
                mensaje = "La materia ya fue asignada por otra persona. Recarga e inténtalo nuevamente."
            });
        }

        return Ok(await ObtenerMateria(ofertaMateriaId, cancellationToken));
    }

    [HttpDelete("materias/{ofertaMateriaId:guid}/titular")]
    [Authorize(Roles = Roles.Administrador + "," + Roles.Jefatura)]
    public async Task<ActionResult<CargaMateriaDto>> QuitarTitular(
        Guid ofertaMateriaId,
        QuitarTitularCargaAcademicaRequest request,
        CancellationToken cancellationToken)
    {
        var oferta = await dbContext.OfertasMaterias
            .Include(x => x.Materia)
            .Include(x => x.Grupo)
                .ThenInclude(x => x.PeriodoCarrera)
                .ThenInclude(x => x.Periodo)
            .SingleOrDefaultAsync(
                x => x.Id == ofertaMateriaId && x.Activa,
                cancellationToken);
        if (oferta is null)
        {
            return NotFound(new
            {
                mensaje = "La materia ya no forma parte de la oferta académica."
            });
        }
        if (!usuarioActual.PuedeAccederCarrera(
                oferta.Grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (oferta.Grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
        {
            return Conflict(new
            {
                mensaje = "No se puede modificar la carga de un periodo cerrado."
            });
        }

        var asignacion = await dbContext.CargasAcademicas
            .SingleOrDefaultAsync(
                x => x.OfertaMateriaId == ofertaMateriaId,
                cancellationToken);
        if (asignacion is null)
            return Conflicto();
        if (asignacion.Estado == EstadoCarga.Autorizada)
        {
            return Conflict(new
            {
                mensaje = "No se puede quitar un titular de una carga ya autorizada por Subdirección."
            });
        }
        if (asignacion.Id != request.CargaAcademicaId
            || !VersionCoincide(asignacion, request.RowVersion))
            return Conflicto();

        dbContext.CargasAcademicas.Remove(asignacion);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflicto();
        }

        return Ok(MapearMateria(oferta, null));
    }

    [HttpPost("materias/{ofertaMateriaId:guid}/autorizar")]
    [Authorize(Roles = Roles.Administrador + "," + Roles.Subdireccion)]
    public async Task<ActionResult<CargaMateriaDto>> Autorizar(
        Guid ofertaMateriaId,
        AutorizarCargaAcademicaRequest request,
        CancellationToken cancellationToken)
    {
        var oferta = await dbContext.OfertasMaterias
            .Include(x => x.Materia)
            .Include(x => x.Grupo)
                .ThenInclude(x => x.PeriodoCarrera)
                .ThenInclude(x => x.Periodo)
            .SingleOrDefaultAsync(
                x => x.Id == ofertaMateriaId && x.Activa,
                cancellationToken);
        if (oferta is null)
            return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(
                oferta.Grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (oferta.Grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
        {
            return Conflict(new
            {
                mensaje = "No se puede autorizar la carga de un periodo cerrado."
            });
        }

        var asignacion = await dbContext.CargasAcademicas
            .SingleOrDefaultAsync(
                x => x.OfertaMateriaId == ofertaMateriaId,
                cancellationToken);
        if (asignacion is null)
        {
            return BadRequest(new
            {
                mensaje = "Asigna un docente titular antes de autorizar la materia."
            });
        }
        if (asignacion.Id != request.CargaAcademicaId
            || !VersionCoincide(asignacion, request.RowVersion))
            return Conflicto();

        var errorDocente = await ValidarDocente(
            oferta, asignacion.DocenteId, cancellationToken);
        if (errorDocente is not null)
            return BadRequest(new { mensaje = errorDocente });

        asignacion.Estado = EstadoCarga.Autorizada;
        asignacion.FechaAutorizacion = DateTime.UtcNow;
        asignacion.UsuarioAutoriza = usuarioActual.UsuarioId;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflicto();
        }

        return Ok(await ObtenerMateria(ofertaMateriaId, cancellationToken));
    }

    private async Task<string?> ValidarDocente(
        OfertaMateria oferta,
        Guid docenteId,
        CancellationToken cancellationToken)
    {
        var docente = await dbContext.Docentes.AsNoTracking()
            .Where(x => x.Id == docenteId
                && x.Activo
                && x.Carreras.Any(c =>
                    c.CarreraId == oferta.Grupo.PeriodoCarrera.CarreraId))
            .Select(x => new
            {
                x.Tipo,
                x.CargaMaximaSemanal
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (docente is null)
            return "El docente debe estar activo y vinculado a la carrera.";

        var disponibilidad = await dbContext.DisponibilidadesDocentes
            .AsNoTracking()
            .Where(x => x.DocenteId == docenteId
                && x.PeriodoId == oferta.Grupo.PeriodoCarrera.PeriodoId
                && x.Validada)
            .Select(x => new
            {
                HorasDisponibles = x.Bloques.Count(b => b.Disponible)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (disponibilidad is null)
        {
            return "El docente no tiene disponibilidad validada para este periodo.";
        }

        int? horasDisponibles = docente.Tipo == TipoDocente.Asignatura
            ? disponibilidad.HorasDisponibles
            : null;
        if (docente.Tipo == TipoDocente.Asignatura && horasDisponibles == 0)
            return "El docente de asignatura no tiene bloques disponibles validados para este periodo.";

        var horasAsignadas = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => x.DocenteId == docenteId
                && x.OfertaMateriaId != oferta.Id
                && x.OfertaMateria.Activa
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId
                    == oferta.Grupo.PeriodoCarrera.PeriodoId)
            .SumAsync(
                x => (int?)x.OfertaMateria.HorasRequeridas,
                cancellationToken)
            ?? 0;
        var nuevaCarga = horasAsignadas + oferta.HorasRequeridas;
        if (docente.Tipo == TipoDocente.TiempoCompleto
            && nuevaCarga > docente.CargaMaximaSemanal)
        {
            return $"La asignación llevaría al docente a {nuevaCarga} h frente a grupo y supera su jornada institucional de {docente.CargaMaximaSemanal} h.";
        }
        if (horasDisponibles.HasValue && nuevaCarga > horasDisponibles.Value)
        {
            return $"La asignación llevaría al docente a {nuevaCarga} h, pero su disponibilidad validada sólo contiene {horasDisponibles.Value} bloques.";
        }

        return null;
    }

    private async Task<CargaMateriaDto> ObtenerMateria(
        Guid ofertaMateriaId,
        CancellationToken cancellationToken)
    {
        var oferta = await dbContext.OfertasMaterias.AsNoTracking()
            .Include(x => x.Materia)
            .SingleAsync(x => x.Id == ofertaMateriaId, cancellationToken);
        var asignacion = await dbContext.CargasAcademicas.AsNoTracking()
            .Include(x => x.Docente)
            .SingleOrDefaultAsync(
                x => x.OfertaMateriaId == ofertaMateriaId,
                cancellationToken);
        return MapearMateria(oferta, asignacion);
    }

    private static CargaConfiguracionDto Mapear(
        PeriodoCarrera configuracion,
        IReadOnlyCollection<CargaAcademica> asignaciones,
        IReadOnlyList<CargaDocenteResumenDto> resumenDocentes) => new(
        configuracion.Id,
        configuracion.PeriodoId,
        configuracion.CarreraId,
        configuracion.ModalidadId,
        configuracion.Carrera.Nombre,
        configuracion.Modalidad.Nombre,
        configuracion.Grupos
            .OrderBy(x => x.Semestre)
            .ThenBy(x => x.Clave)
            .Select(grupo => new CargaGrupoDto(
                grupo.Id,
                grupo.Semestre,
                grupo.Clave,
                grupo.Nombre,
                grupo.EspacioBase is null
                    ? null
                    : $"{grupo.EspacioBase.Clave} · {grupo.EspacioBase.Nombre}",
                grupo.Oferta
                    .Where(x => x.Activa)
                    .OrderBy(x => x.Materia.Nombre)
                    .Select(oferta => MapearMateria(
                        oferta,
                        asignaciones.SingleOrDefault(
                            x => x.OfertaMateriaId == oferta.Id)))
                    .ToArray()))
            .ToArray(),
        resumenDocentes);

    private static CargaMateriaDto MapearMateria(
        OfertaMateria oferta,
        CargaAcademica? asignacion) => new(
        oferta.Id,
        oferta.MateriaId,
        oferta.Materia.Clave,
        oferta.Materia.Nombre,
        oferta.HorasRequeridas,
        oferta.Materia.HorasTeoricas,
        oferta.Materia.HorasPracticas,
        asignacion is null
            ? null
            : new CargaTitularDto(
                asignacion.Id,
                asignacion.DocenteId,
                asignacion.Docente.Apellidos + ", " + asignacion.Docente.Nombres,
                (byte)asignacion.Estado,
                asignacion.Estado switch
                {
                    EstadoCarga.Autorizada => "Autorizada",
                    EstadoCarga.Devuelta => "Devuelta",
                    _ => "Borrador"
                },
                asignacion.Observaciones,
                Convert.ToBase64String(asignacion.RowVersion)),
        asignacion?.Estado == EstadoCarga.Autorizada);

    private static bool VersionCoincide(
        CargaAcademica? asignacion,
        string? rowVersion)
    {
        if (asignacion is null)
            return string.IsNullOrWhiteSpace(rowVersion);
        if (string.IsNullOrWhiteSpace(rowVersion))
            return false;

        try
        {
            return Convert.FromBase64String(rowVersion)
                .SequenceEqual(asignacion.RowVersion);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private bool TieneAlcanceInstitucional() =>
        usuarioActual.TieneRol(Roles.Administrador)
        || usuarioActual.TieneRol(Roles.Subdireccion);

    private Guid[] CarrerasUsuario() => User.FindAll("carrera")
        .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
        .Where(x => x != Guid.Empty)
        .ToArray();

    private ConflictObjectResult Conflicto() => Conflict(new
    {
        mensaje = "La carga fue modificada por otra persona. Recarga e inténtalo nuevamente."
    });
}
