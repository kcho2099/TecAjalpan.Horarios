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

        var carrerasPermitidas = carreras.Select(x => x.Id).ToArray();
        var docentesEntidades = await dbContext.Docentes.AsNoTracking()
            .Include(x => x.Carreras)
            .Where(x => x.Activo
                && x.Carreras.Any(c => carrerasPermitidas.Contains(c.CarreraId)))
            .OrderBy(x => x.Apellidos)
            .ThenBy(x => x.Nombres)
            .ToArrayAsync(cancellationToken);

        var docentes = docentesEntidades
            .Select(x => new CargaDocenteDto(
                x.Id,
                x.NumeroTrabajador,
                x.Apellidos + ", " + x.Nombres,
                x.CargaMaximaSemanal,
                x.Carreras.Select(c => c.CarreraId).ToArray()))
            .ToArray();

        return Ok(new CargaAcademicaCatalogosDto(
            periodos, carreras, modalidades, docentes));
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
            .Where(x => x.CarreraId == carreraId && x.Docente.Activo)
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

        var disponibilidadesAsignatura = await dbContext.DisponibilidadesDocentes
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

        var autorizacionesAsignatura = await dbContext.AutorizacionesCargaDocentes
            .AsNoTracking()
            .Where(x => docentesCarreraIds.Contains(x.DocenteId)
                && x.PeriodoId == periodoId)
            .Select(x => new
            {
                x.DocenteId,
                x.HorasAutorizadas,
                x.RowVersion
            })
            .ToArrayAsync(cancellationToken);

        var resumenDocentes = docentesCarrera
            .Select(docente =>
            {
                var disponibilidad = disponibilidadesAsignatura
                    .SingleOrDefault(x => x.DocenteId == docente.DocenteId);
                var autorizacion = autorizacionesAsignatura
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
                        ? autorizacion?.HorasAutorizadas
                        : null,
                    docente.Tipo == TipoDocente.Asignatura
                        ? disponibilidad?.HorasDisponibles
                        : null,
                    autorizacion is null
                        ? null
                        : Convert.ToBase64String(autorizacion.RowVersion));
            })
            .OrderByDescending(x => x.HorasAsignadas)
            .ThenBy(x => x.DocenteNombre)
            .ToArray();

        return Ok(Mapear(configuracion, asignaciones, resumenDocentes));
    }

    [HttpPut("docentes/{docenteId:guid}/autorizacion/{periodoId:guid}")]
    [Authorize(Roles = Roles.Administrador + "," + Roles.Jefatura)]
    public async Task<ActionResult<CargaAutorizacionDocenteDto>>
        GuardarCargaAutorizada(
            Guid docenteId,
            Guid periodoId,
            GuardarCargaAutorizadaRequest request,
            CancellationToken cancellationToken)
    {
        if (request.DocenteId != docenteId || request.PeriodoId != periodoId)
            return BadRequest(new { mensaje = "El docente o periodo no coincide con la ruta." });

        var periodoAbierto = await dbContext.Periodos.AsNoTracking()
            .AnyAsync(x => x.Id == periodoId && x.Estado != EstadoPeriodo.Cerrado,
                cancellationToken);
        if (!periodoAbierto)
            return Conflict(new { mensaje = "No se puede modificar la autorización de un periodo cerrado." });

        var docente = await dbContext.Docentes.AsNoTracking()
            .Where(x => x.Id == docenteId && x.Activo)
            .Select(x => new
            {
                x.Tipo,
                CarreraIds = x.Carreras.Select(c => c.CarreraId).ToArray()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (docente is null)
            return NotFound(new { mensaje = "No se encontró un docente activo." });
        if (docente.Tipo != TipoDocente.Asignatura)
            return BadRequest(new { mensaje = "La autorización por periodo sólo aplica a docentes de asignatura." });
        if (!TieneAlcanceInstitucional()
            && !docente.CarreraIds.Any(usuarioActual.PuedeAccederCarrera))
            return Forbid();

        var autorizacion = await dbContext.AutorizacionesCargaDocentes
            .SingleOrDefaultAsync(x => x.DocenteId == docenteId
                && x.PeriodoId == periodoId, cancellationToken);
        if (!VersionCoincide(autorizacion?.RowVersion, request.RowVersion))
            return Conflicto();

        if (autorizacion is null)
        {
            autorizacion = new AutorizacionCargaDocente
            {
                DocenteId = docenteId,
                PeriodoId = periodoId
            };
            dbContext.AutorizacionesCargaDocentes.Add(autorizacion);
        }

        var horasYaAsignadas = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => x.DocenteId == docenteId
                && x.OfertaMateria.Activa
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == periodoId)
            .SumAsync(x => (int?)x.OfertaMateria.HorasRequeridas, cancellationToken)
            ?? 0;
        if (request.HorasAutorizadas < horasYaAsignadas)
        {
            return BadRequest(new
            {
                mensaje = $"El docente ya tiene {horasYaAsignadas} h asignadas; la autorización no puede ser menor."
            });
        }

        autorizacion.HorasAutorizadas = request.HorasAutorizadas;
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
            return Conflict(new { mensaje = "La autorización fue registrada por otra persona. Recarga e inténtalo nuevamente." });
        }

        return Ok(new CargaAutorizacionDocenteDto(
            autorizacion.DocenteId,
            autorizacion.PeriodoId,
            autorizacion.HorasAutorizadas,
            Convert.ToBase64String(autorizacion.RowVersion)));
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

        var cargaMaximaSemanal = docente.CargaMaximaSemanal;
        int? horasDisponibles = null;
        if (docente.Tipo == TipoDocente.Asignatura)
        {
            var autorizacion = await dbContext.AutorizacionesCargaDocentes
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.DocenteId == docenteId
                    && x.PeriodoId == oferta.Grupo.PeriodoCarrera.PeriodoId,
                    cancellationToken);
            if (autorizacion is null || autorizacion.HorasAutorizadas == 0)
            {
                return "Define primero las horas autorizadas del docente de asignatura para este periodo.";
            }

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
            if (disponibilidad is null || disponibilidad.HorasDisponibles == 0)
            {
                return "El docente de asignatura no tiene disponibilidad validada para este periodo.";
            }

            cargaMaximaSemanal = autorizacion.HorasAutorizadas;
            horasDisponibles = disponibilidad.HorasDisponibles;
        }

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
        if (nuevaCarga > cargaMaximaSemanal)
        {
            var limite = docente.Tipo == TipoDocente.Asignatura
                ? "autorizadas para este periodo"
                : "de carga máxima semanal";
            return $"La asignación llevaría al docente a {nuevaCarga} h y supera sus {cargaMaximaSemanal} h {limite}.";
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

    private static bool VersionCoincide(byte[]? versionActual, string? rowVersion)
    {
        if (versionActual is null)
            return string.IsNullOrWhiteSpace(rowVersion);
        if (string.IsNullOrWhiteSpace(rowVersion))
            return false;

        try
        {
            return Convert.FromBase64String(rowVersion).SequenceEqual(versionActual);
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
