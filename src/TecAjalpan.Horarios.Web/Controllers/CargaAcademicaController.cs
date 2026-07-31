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
            var ids = CarrerasUsuario();
            carrerasConsulta = carrerasConsulta.Where(x => ids.Contains(x.Id));
        }

        var carreras = await carrerasConsulta.OrderBy(x => x.Nombre)
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
            .Where(x => x.Activo && x.Carreras.Any(c => carrerasPermitidas.Contains(c.CarreraId)))
            .OrderBy(x => x.Apellidos).ThenBy(x => x.Nombres)
            .ToArrayAsync(cancellationToken);
        var docentes = docentesEntidades.Select(x => new CargaDocenteDto(
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
            return NotFound(new { mensaje = "No existe oferta académica para la selección indicada." });

        var ofertasIds = configuracion.Grupos
            .SelectMany(x => x.Oferta)
            .Select(x => x.Id)
            .ToArray();
        var asignaciones = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => ofertasIds.Contains(x.OfertaMateriaId))
            .Include(x => x.Docente)
            .OrderBy(x => x.Rol)
            .ToArrayAsync(cancellationToken);

        var docentesCarreraIds = await dbContext.DocentesCarreras.AsNoTracking()
            .Where(x => x.CarreraId == carreraId && x.Docente.Activo)
            .Select(x => x.DocenteId)
            .ToArrayAsync(cancellationToken);
        var resumenDocentes = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => docentesCarreraIds.Contains(x.DocenteId)
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == periodoId)
            .GroupBy(x => new
            {
                x.DocenteId,
                x.Docente.Apellidos,
                x.Docente.Nombres,
                x.Docente.CargaMaximaSemanal
            })
            .Select(x => new CargaDocenteResumenDto(
                x.Key.DocenteId,
                x.Key.Apellidos + ", " + x.Key.Nombres,
                x.Sum(c => c.HorasAsignadas),
                x.Key.CargaMaximaSemanal))
            .OrderByDescending(x => x.HorasAsignadas)
            .ThenBy(x => x.DocenteNombre)
            .ToArrayAsync(cancellationToken);

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
            return BadRequest(new { mensaje = "La materia enviada no coincide con la ruta." });

        var oferta = await dbContext.OfertasMaterias
            .Include(x => x.Materia)
            .Include(x => x.Grupo).ThenInclude(x => x.PeriodoCarrera).ThenInclude(x => x.Periodo)
            .SingleOrDefaultAsync(x => x.Id == ofertaMateriaId && x.Activa, cancellationToken);
        if (oferta is null)
            return NotFound(new { mensaje = "La materia ya no forma parte de la oferta académica." });
        if (!usuarioActual.PuedeAccederCarrera(oferta.Grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (oferta.Grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
            return Conflict(new { mensaje = "No se puede modificar la carga de un periodo cerrado." });

        var error = await ValidarSolicitud(oferta, request, cancellationToken);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var existentes = await dbContext.CargasAcademicas.IgnoreQueryFilters()
            .Where(x => x.OfertaMateriaId == ofertaMateriaId)
            .ToListAsync(cancellationToken);
        if (!VersionesCoinciden(existentes.Where(x => !x.Eliminado), request.Versiones))
            return Conflicto();

        AplicarAsignacion(
            existentes,
            ofertaMateriaId,
            RolCargaAcademica.Titular,
            request.DocenteTitularId,
            checked((byte)request.HorasTitular),
            request.Observaciones);

        if (request.DocentePracticasId.HasValue)
        {
            AplicarAsignacion(
                existentes,
                ofertaMateriaId,
                RolCargaAcademica.PracticasLaboratorio,
                request.DocentePracticasId.Value,
                checked((byte)request.HorasPracticas),
                request.Observaciones);
        }
        else
        {
            var practica = existentes.FirstOrDefault(x =>
                !x.Eliminado && x.Rol == RolCargaAcademica.PracticasLaboratorio);
            if (practica is not null)
                dbContext.CargasAcademicas.Remove(practica);
        }

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflicto(); }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "No fue posible guardar la carga académica. Recarga e inténtalo nuevamente." });
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
            .Include(x => x.Grupo).ThenInclude(x => x.PeriodoCarrera).ThenInclude(x => x.Periodo)
            .SingleOrDefaultAsync(x => x.Id == ofertaMateriaId && x.Activa, cancellationToken);
        if (oferta is null) return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(oferta.Grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (oferta.Grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
            return Conflict(new { mensaje = "No se puede autorizar la carga de un periodo cerrado." });

        var asignaciones = await dbContext.CargasAcademicas
            .Where(x => x.OfertaMateriaId == ofertaMateriaId)
            .ToListAsync(cancellationToken);
        if (!VersionesCoinciden(asignaciones, request.Versiones)) return Conflicto();
        if (!EsCompleta(oferta, asignaciones))
            return BadRequest(new { mensaje = "La materia debe tener titular y todas sus horas asignadas antes de autorizarla." });

        foreach (var asignacion in asignaciones)
        {
            asignacion.Estado = EstadoCarga.Autorizada;
            asignacion.FechaAutorizacion = DateTime.UtcNow;
            asignacion.UsuarioAutoriza = usuarioActual.UsuarioId;
        }

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflicto(); }
        return Ok(await ObtenerMateria(ofertaMateriaId, cancellationToken));
    }

    private async Task<string?> ValidarSolicitud(
        OfertaMateria oferta,
        GuardarCargaAcademicaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DocenteTitularId == Guid.Empty)
            return "Selecciona al docente titular.";
        if (request.DocentePracticasId == request.DocenteTitularId)
            return "El responsable de prácticas debe ser distinto del docente titular.";
        if (request.DocentePracticasId.HasValue && oferta.Materia.HorasPracticas == 0)
            return "Esta materia no tiene horas prácticas para asignar a otro responsable.";
        if (request.DocentePracticasId.HasValue && request.HorasPracticas <= 0)
            return "Indica las horas que atenderá el responsable de prácticas.";
        if (!request.DocentePracticasId.HasValue && request.HorasPracticas != 0)
            return "Selecciona al responsable de prácticas o establece sus horas en cero.";
        if (request.HorasPracticas > oferta.Materia.HorasPracticas)
            return $"La materia sólo tiene {oferta.Materia.HorasPracticas} hora(s) prácticas.";
        if (request.HorasTitular + request.HorasPracticas != oferta.HorasRequeridas)
            return $"Debes distribuir exactamente las {oferta.HorasRequeridas} hora(s) semanales de la materia.";

        var carreraId = oferta.Grupo.PeriodoCarrera.CarreraId;
        var docentesIds = new List<Guid> { request.DocenteTitularId };
        if (request.DocentePracticasId.HasValue)
            docentesIds.Add(request.DocentePracticasId.Value);
        var docentes = await dbContext.Docentes.AsNoTracking()
            .Where(x => docentesIds.Contains(x.Id)
                && x.Activo
                && x.Carreras.Any(c => c.CarreraId == carreraId))
            .Select(x => new { x.Id, x.CargaMaximaSemanal })
            .ToArrayAsync(cancellationToken);
        if (docentes.Length != docentesIds.Length)
            return "Todos los docentes deben estar activos y vinculados a la carrera.";

        var periodoId = oferta.Grupo.PeriodoCarrera.PeriodoId;
        var cargasActuales = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => docentesIds.Contains(x.DocenteId)
                && x.OfertaMateriaId != oferta.Id
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == periodoId)
            .GroupBy(x => x.DocenteId)
            .Select(x => new { DocenteId = x.Key, Horas = x.Sum(c => c.HorasAsignadas) })
            .ToDictionaryAsync(x => x.DocenteId, x => x.Horas, cancellationToken);
        var solicitadas = new Dictionary<Guid, int>
        {
            [request.DocenteTitularId] = request.HorasTitular
        };
        if (request.DocentePracticasId.HasValue)
            solicitadas[request.DocentePracticasId.Value] = request.HorasPracticas;
        foreach (var docente in docentes)
        {
            var total = cargasActuales.GetValueOrDefault(docente.Id) + solicitadas[docente.Id];
            if (total > docente.CargaMaximaSemanal)
                return $"La asignación supera la carga máxima semanal del docente ({docente.CargaMaximaSemanal} h).";
        }
        return null;
    }

    private void AplicarAsignacion(
        List<CargaAcademica> existentes,
        Guid ofertaMateriaId,
        RolCargaAcademica rol,
        Guid docenteId,
        byte horas,
        string? observaciones)
    {
        var asignacion = existentes.FirstOrDefault(x => !x.Eliminado && x.Rol == rol)
            ?? existentes.FirstOrDefault(x => x.Eliminado && x.Rol == rol);
        if (asignacion is null)
        {
            asignacion = new CargaAcademica { OfertaMateriaId = ofertaMateriaId, Rol = rol };
            dbContext.CargasAcademicas.Add(asignacion);
            existentes.Add(asignacion);
        }
        else if (asignacion.Eliminado)
        {
            asignacion.Eliminado = false;
            asignacion.FechaElimina = null;
            asignacion.UsuarioElimina = null;
        }

        asignacion.DocenteId = docenteId;
        asignacion.HorasAsignadas = horas;
        asignacion.Observaciones = string.IsNullOrWhiteSpace(observaciones)
            ? null
            : observaciones.Trim();
        asignacion.Estado = EstadoCarga.Borrador;
        asignacion.FechaAutorizacion = null;
        asignacion.UsuarioAutoriza = null;
    }

    private async Task<CargaMateriaDto> ObtenerMateria(
        Guid ofertaMateriaId,
        CancellationToken cancellationToken)
    {
        var oferta = await dbContext.OfertasMaterias.AsNoTracking()
            .Include(x => x.Materia)
            .SingleAsync(x => x.Id == ofertaMateriaId, cancellationToken);
        var asignaciones = await dbContext.CargasAcademicas.AsNoTracking()
            .Include(x => x.Docente)
            .Where(x => x.OfertaMateriaId == ofertaMateriaId)
            .OrderBy(x => x.Rol)
            .ToArrayAsync(cancellationToken);
        return MapearMateria(oferta, asignaciones);
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
        configuracion.Grupos.OrderBy(x => x.Semestre).ThenBy(x => x.Clave)
            .Select(grupo => new CargaGrupoDto(
                grupo.Id,
                grupo.Semestre,
                grupo.Clave,
                grupo.Nombre,
                grupo.EspacioBase is null
                    ? null
                    : $"{grupo.EspacioBase.Clave} · {grupo.EspacioBase.Nombre}",
                grupo.Oferta.Where(x => x.Activa)
                    .OrderBy(x => x.Materia.Nombre)
                    .Select(oferta => MapearMateria(
                        oferta,
                        asignaciones.Where(x => x.OfertaMateriaId == oferta.Id).ToArray()))
                    .ToArray()))
            .ToArray(),
        resumenDocentes);

    private static CargaMateriaDto MapearMateria(
        OfertaMateria oferta,
        IReadOnlyCollection<CargaAcademica> asignaciones)
    {
        var completa = EsCompleta(oferta, asignaciones);
        return new(
            oferta.Id,
            oferta.MateriaId,
            oferta.Materia.Clave,
            oferta.Materia.Nombre,
            oferta.HorasRequeridas,
            oferta.Materia.HorasTeoricas,
            oferta.Materia.HorasPracticas,
            asignaciones.OrderBy(x => x.Rol).Select(x => new CargaAsignacionDto(
                x.Id,
                x.DocenteId,
                x.Docente.Apellidos + ", " + x.Docente.Nombres,
                (byte)x.Rol,
                x.Rol == RolCargaAcademica.Titular ? "Titular" : "Prácticas/Laboratorio",
                x.HorasAsignadas,
                (byte)x.Estado,
                x.Estado switch
                {
                    EstadoCarga.Autorizada => "Autorizada",
                    EstadoCarga.Devuelta => "Devuelta",
                    _ => "Borrador"
                },
                x.Observaciones,
                Convert.ToBase64String(x.RowVersion))).ToArray(),
            completa,
            completa && asignaciones.All(x => x.Estado == EstadoCarga.Autorizada));
    }

    private static bool EsCompleta(
        OfertaMateria oferta,
        IEnumerable<CargaAcademica> asignaciones)
    {
        var activas = asignaciones.Where(x => !x.Eliminado).ToArray();
        return activas.Any(x => x.Rol == RolCargaAcademica.Titular)
            && activas.Sum(x => x.HorasAsignadas) == oferta.HorasRequeridas;
    }

    private static bool VersionesCoinciden(
        IEnumerable<CargaAcademica> existentes,
        IEnumerable<CargaRowVersionDto> versiones)
    {
        var actuales = existentes.ToArray();
        var listaVersiones = versiones.ToArray();
        if (listaVersiones.Select(x => x.Id).Distinct().Count() != listaVersiones.Length)
            return false;
        var recibidas = listaVersiones.ToDictionary(x => x.Id, x => x.RowVersion);
        if (actuales.Length != recibidas.Count) return false;
        foreach (var actual in actuales)
        {
            if (!recibidas.TryGetValue(actual.Id, out var valor)) return false;
            try
            {
                if (!Convert.FromBase64String(valor).SequenceEqual(actual.RowVersion)) return false;
            }
            catch (FormatException) { return false; }
        }
        return true;
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
