using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.CargaAcademica;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Domain.Rules;
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
        var modulosOferta = await dbContext.ModulosMaterias.AsNoTracking()
            .Where(x => x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == periodoId)
            .Select(x => new ModuloCargaInfo(
                x.OfertaMateriaId,
                x.ModuloSabatino.Orden,
                x.ModuloSabatino.Semanas,
                x.ModuloSabatino.FechaInicio,
                x.ModuloSabatino.FechaFin,
                x.Turno))
            .ToDictionaryAsync(x => x.OfertaMateriaId, cancellationToken);

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
                x.OfertaMateriaId,
                HorasRequeridas = (int)x.OfertaMateria.HorasRequeridas,
                TipoModalidad = x.OfertaMateria.Grupo.PeriodoCarrera.Modalidad.Tipo
            })
            .ToArrayAsync(cancellationToken);

        var disponibilidadesValidadas = await dbContext.DisponibilidadesDocentes
            .AsNoTracking()
            .Include(x => x.Bloques)
            .Include(x => x.Jornadas)
            .Where(x => docentesCarreraIds.Contains(x.DocenteId)
                && x.PeriodoId == periodoId
                && x.Validada)
            .ToArrayAsync(cancellationToken);

        var resumenDocentes = docentesCarrera
            .Where(docente =>
            {
                var disponibilidad = disponibilidadesValidadas
                    .SingleOrDefault(x => x.DocenteId == docente.DocenteId);
                return disponibilidad is not null
                    && CalcularHorasDisponibles(
                        docente.Tipo,
                        disponibilidad,
                        configuracion.Modalidad.Tipo) > 0;
            })
            .Select(docente =>
            {
                var disponibilidad = disponibilidadesValidadas
                    .Single(x => x.DocenteId == docente.DocenteId);
                var cargas = cargasDocentes
                    .Where(x => x.DocenteId == docente.DocenteId)
                    .ToArray();
                var horasEscolarizadas = cargas
                    .Where(x => x.TipoModalidad == TipoModalidad.Escolarizada)
                    .Sum(x => x.HorasRequeridas);
                var cargasSabatinas = cargas
                    .Where(x => x.TipoModalidad == TipoModalidad.Sabatina
                        && modulosOferta.ContainsKey(x.OfertaMateriaId))
                    .GroupBy(x => new
                    {
                        modulosOferta[x.OfertaMateriaId].Modulo,
                        modulosOferta[x.OfertaMateriaId].FechaInicio,
                        modulosOferta[x.OfertaMateriaId].FechaFin
                    })
                    .Select(x => new CargaDocenteModuloDto(
                        x.Key.Modulo,
                        x.Key.FechaInicio,
                        x.Key.FechaFin,
                        x.Count() * 4,
                        x.Any(c => modulosOferta[c.OfertaMateriaId].Turno == TurnoSabatino.Matutino),
                        x.Any(c => modulosOferta[c.OfertaMateriaId].Turno == TurnoSabatino.Vespertino)))
                    .OrderBy(x => x.FechaInicio)
                    .ToArray();
                var maximoSabatino = CalcularMaximoSabatino(cargasSabatinas);
                return new CargaDocenteResumenDto(
                    docente.DocenteId,
                    docente.Apellidos + ", " + docente.Nombres,
                    (byte)docente.Tipo,
                    horasEscolarizadas + maximoSabatino,
                    docente.CargaMaximaSemanal,
                    docente.Tipo == TipoDocente.Asignatura
                        ? disponibilidad.Bloques.Count(x => x.Disponible)
                        : null,
                    cargas
                        .Where(x => configuracion.Modalidad.Tipo == TipoModalidad.Escolarizada
                            && x.TipoModalidad == TipoModalidad.Escolarizada)
                        .Sum(x => x.HorasRequeridas)
                        + (configuracion.Modalidad.Tipo == TipoModalidad.Sabatina
                            ? maximoSabatino
                            : 0),
                    CalcularHorasDisponibles(
                        docente.Tipo,
                        disponibilidad,
                        configuracion.Modalidad.Tipo),
                    horasEscolarizadas,
                    cargasSabatinas,
                    DisponibleEnTurnoSabatino(
                        docente.Tipo, disponibilidad, TurnoSabatino.Matutino),
                    DisponibleEnTurnoSabatino(
                        docente.Tipo, disponibilidad, TurnoSabatino.Vespertino));
            })
            .OrderByDescending(x => x.HorasAsignadas)
            .ThenBy(x => x.DocenteNombre)
            .ToArray();

        return Ok(Mapear(configuracion, asignaciones, resumenDocentes, modulosOferta));
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
            .Include(x => x.Grupo)
                .ThenInclude(x => x.PeriodoCarrera)
                .ThenInclude(x => x.Modalidad)
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

        return Ok(MapearMateria(
            oferta,
            null,
            await ObtenerModuloOferta(ofertaMateriaId, cancellationToken)));
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
            .Include(x => x.Grupo)
                .ThenInclude(x => x.PeriodoCarrera)
                .ThenInclude(x => x.Modalidad)
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
            .Include(x => x.Bloques)
            .Include(x => x.Jornadas)
            .Where(x => x.DocenteId == docenteId
                && x.PeriodoId == oferta.Grupo.PeriodoCarrera.PeriodoId
                && x.Validada)
            .SingleOrDefaultAsync(cancellationToken);
        if (disponibilidad is null)
        {
            return "El docente no tiene disponibilidad validada para este periodo.";
        }

        var tipoModalidad = oferta.Grupo.PeriodoCarrera.Modalidad.Tipo;
        var moduloObjetivo = tipoModalidad == TipoModalidad.Sabatina
            ? await ObtenerModuloOferta(oferta.Id, cancellationToken)
            : null;
        if (tipoModalidad == TipoModalidad.Sabatina && moduloObjetivo is null)
            return "Configura primero el módulo y turno sabatino de la materia en Oferta académica.";
        var horasDisponiblesModalidad = CalcularHorasDisponibles(
            docente.Tipo,
            disponibilidad,
            tipoModalidad);
        if (horasDisponiblesModalidad == 0)
        {
            return tipoModalidad == TipoModalidad.Sabatina
                ? "El docente no tiene disponibilidad validada para asistir los sábados."
                : "El docente no tiene disponibilidad validada de lunes a viernes.";
        }
        if (moduloObjetivo is not null
            && !DisponibleEnTurnoSabatino(docente.Tipo, disponibilidad, moduloObjetivo.Turno))
        {
            return $"La disponibilidad del docente no cubre completa la franja {NombreTurno(moduloObjetivo.Turno)}.";
        }

        var cargasAsignadas = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => x.DocenteId == docenteId
                && x.OfertaMateriaId != oferta.Id
                && x.OfertaMateria.Activa
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId
                    == oferta.Grupo.PeriodoCarrera.PeriodoId)
            .Select(x => new
            {
                x.OfertaMateriaId,
                Horas = (int)x.OfertaMateria.HorasRequeridas,
                TipoModalidad = x.OfertaMateria.Grupo.PeriodoCarrera.Modalidad.Tipo
            })
            .ToArrayAsync(cancellationToken);
        var idsSabatinos = cargasAsignadas
            .Where(x => x.TipoModalidad == TipoModalidad.Sabatina)
            .Select(x => x.OfertaMateriaId)
            .ToArray();
        var modulosCargas = await dbContext.ModulosMaterias.AsNoTracking()
            .Where(x => idsSabatinos.Contains(x.OfertaMateriaId))
            .Select(x => new ModuloCargaInfo(
                x.OfertaMateriaId,
                x.ModuloSabatino.Orden,
                x.ModuloSabatino.Semanas,
                x.ModuloSabatino.FechaInicio,
                x.ModuloSabatino.FechaFin,
                x.Turno))
            .ToDictionaryAsync(x => x.OfertaMateriaId, cancellationToken);
        var horasEscolarizadas = cargasAsignadas
            .Where(x => x.TipoModalidad == TipoModalidad.Escolarizada)
            .Sum(x => x.Horas);
        var horasNuevaMateria = tipoModalidad == TipoModalidad.Sabatina
            ? 4
            : oferta.HorasRequeridas;
        var horasSabatinasModulo = moduloObjetivo is null
            ? 0
            : CalcularMaximoEnIntervalo(
                modulosCargas.Values,
                moduloObjetivo.FechaInicio,
                moduloObjetivo.FechaFin);
        var intervalosSabatinos = cargasAsignadas
            .Where(x => x.TipoModalidad == TipoModalidad.Sabatina
                && modulosCargas.ContainsKey(x.OfertaMateriaId))
            .Select(x => modulosCargas[x.OfertaMateriaId])
            .ToList();
        if (moduloObjetivo is not null)
        {
            var conflictoTurno = cargasAsignadas.Any(x =>
                x.TipoModalidad == TipoModalidad.Sabatina
                && modulosCargas.TryGetValue(x.OfertaMateriaId, out var modulo)
                && SeTraslapan(
                    modulo.FechaInicio, modulo.FechaFin,
                    moduloObjetivo.FechaInicio, moduloObjetivo.FechaFin)
                && modulo.Turno == moduloObjetivo.Turno);
            if (conflictoTurno)
                return $"El docente ya tiene otra materia en el módulo {moduloObjetivo.Modulo}, turno {NombreTurno(moduloObjetivo.Turno)}.";
        }

        if (moduloObjetivo is not null)
            intervalosSabatinos.Add(moduloObjetivo);
        var maximoSabatino = CalcularMaximoEnIntervalo(intervalosSabatinos);
        var nuevaCarga = horasEscolarizadas
            + (tipoModalidad == TipoModalidad.Escolarizada ? horasNuevaMateria : 0)
            + maximoSabatino;
        if (docente.Tipo == TipoDocente.TiempoCompleto
            && nuevaCarga > docente.CargaMaximaSemanal)
        {
            return $"La asignación llevaría al docente a {nuevaCarga} h frente a grupo y supera su jornada institucional de {docente.CargaMaximaSemanal} h.";
        }
        var horasAsignadasModalidad = tipoModalidad == TipoModalidad.Sabatina
            ? horasSabatinasModulo
            : horasEscolarizadas;
        var nuevaCargaModalidad = horasAsignadasModalidad + horasNuevaMateria;
        if (nuevaCargaModalidad > horasDisponiblesModalidad)
        {
            var dias = tipoModalidad == TipoModalidad.Sabatina
                ? "del sábado"
                : "de lunes a viernes";
            return $"La asignación llevaría al docente a {nuevaCargaModalidad} h en esta modalidad, pero su disponibilidad validada {dias} sólo permite {horasDisponiblesModalidad} h.";
        }

        return null;
    }

    private static int CalcularHorasDisponibles(
        TipoDocente tipoDocente,
        DisponibilidadDocente disponibilidad,
        TipoModalidad tipoModalidad)
    {
        if (tipoDocente == TipoDocente.Asignatura)
        {
            return disponibilidad.Bloques.Count(x =>
                x.Disponible
                && ReglasModalidad.PermiteProgramar(tipoModalidad, x.Dia));
        }

        return disponibilidad.Jornadas
            .Where(x => ReglasModalidad.PermiteProgramar(tipoModalidad, x.Dia))
            .Sum(jornada => Enumerable.Range(0, 8).Count(bloque =>
            {
                var inicioBloque = new TimeOnly(8 + bloque, 0);
                var finBloque = new TimeOnly(9 + bloque, 0);
                return jornada.HoraInicio <= inicioBloque
                    && jornada.HoraFin >= finBloque;
            }));
    }

    private static bool DisponibleEnTurnoSabatino(
        TipoDocente tipoDocente,
        DisponibilidadDocente disponibilidad,
        TurnoSabatino turno)
    {
        var inicio = turno == TurnoSabatino.Matutino ? 1 : 5;
        if (tipoDocente == TipoDocente.Asignatura)
        {
            return Enumerable.Range(inicio, 4).All(bloque =>
                disponibilidad.Bloques.Any(x =>
                    x.Dia == DiaAcademico.Sabado
                    && x.Bloque == bloque
                    && x.Disponible));
        }

        var horaInicio = turno == TurnoSabatino.Matutino
            ? new TimeOnly(8, 0)
            : new TimeOnly(12, 0);
        var horaFin = turno == TurnoSabatino.Matutino
            ? new TimeOnly(12, 0)
            : new TimeOnly(16, 0);
        return disponibilidad.Jornadas.Any(x =>
            x.Dia == DiaAcademico.Sabado
            && x.HoraInicio <= horaInicio
            && x.HoraFin >= horaFin);
    }

    private static int CalcularMaximoSabatino(
        IReadOnlyCollection<CargaDocenteModuloDto> cargas) =>
        cargas.Count == 0
            ? 0
            : EnumerarSabados(cargas.Min(x => x.FechaInicio), cargas.Max(x => x.FechaFin))
                .Select(fecha => cargas
                    .Where(x => x.FechaInicio <= fecha && x.FechaFin >= fecha)
                    .Sum(x => x.HorasAsignadas))
                .DefaultIfEmpty(0)
                .Max();

    private static int CalcularMaximoEnIntervalo(
        IEnumerable<ModuloCargaInfo> modulos,
        DateOnly? inicio = null,
        DateOnly? fin = null)
    {
        var intervalos = modulos.ToArray();
        if (intervalos.Length == 0) return 0;
        var desde = inicio ?? intervalos.Min(x => x.FechaInicio);
        var hasta = fin ?? intervalos.Max(x => x.FechaFin);
        return EnumerarSabados(desde, hasta)
            .Select(fecha => intervalos.Count(x =>
                x.FechaInicio <= fecha && x.FechaFin >= fecha) * 4)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static IEnumerable<DateOnly> EnumerarSabados(DateOnly inicio, DateOnly fin)
    {
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(7))
            yield return fecha;
    }

    private static bool SeTraslapan(
        DateOnly inicioA, DateOnly finA, DateOnly inicioB, DateOnly finB) =>
        inicioA <= finB && inicioB <= finA;

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
        return MapearMateria(
            oferta,
            asignacion,
            await ObtenerModuloOferta(ofertaMateriaId, cancellationToken));
    }

    private Task<ModuloCargaInfo?> ObtenerModuloOferta(
        Guid ofertaMateriaId,
        CancellationToken cancellationToken) =>
        dbContext.ModulosMaterias.AsNoTracking()
            .Where(x => x.OfertaMateriaId == ofertaMateriaId)
            .Select(x => new ModuloCargaInfo(
                x.OfertaMateriaId,
                x.ModuloSabatino.Orden,
                x.ModuloSabatino.Semanas,
                x.ModuloSabatino.FechaInicio,
                x.ModuloSabatino.FechaFin,
                x.Turno))
            .SingleOrDefaultAsync(cancellationToken);

    private static CargaConfiguracionDto Mapear(
        PeriodoCarrera configuracion,
        IReadOnlyCollection<CargaAcademica> asignaciones,
        IReadOnlyList<CargaDocenteResumenDto> resumenDocentes,
        IReadOnlyDictionary<Guid, ModuloCargaInfo> modulosOferta) => new(
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
                            x => x.OfertaMateriaId == oferta.Id),
                        modulosOferta.GetValueOrDefault(oferta.Id)))
                    .ToArray()))
            .ToArray(),
        resumenDocentes);

    private static CargaMateriaDto MapearMateria(
        OfertaMateria oferta,
        CargaAcademica? asignacion,
        ModuloCargaInfo? modulo) => new(
        oferta.Id,
        oferta.MateriaId,
        oferta.Materia.Clave,
        oferta.Materia.Nombre,
        oferta.HorasRequeridas,
        checked((byte)(modulo is null ? oferta.HorasRequeridas : 4)),
        oferta.Materia.HorasTeoricas,
        oferta.Materia.HorasPracticas,
        modulo?.Modulo,
        modulo?.Semanas,
        modulo?.FechaInicio,
        modulo?.FechaFin,
        modulo is null ? null : (byte)modulo.Turno,
        modulo is null ? null : NombreTurno(modulo.Turno),
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

    private static string NombreTurno(TurnoSabatino turno) =>
        turno == TurnoSabatino.Matutino ? "08:00–12:00" : "12:00–16:00";

    private sealed record ModuloCargaInfo(
        Guid OfertaMateriaId,
        byte Modulo,
        byte Semanas,
        DateOnly FechaInicio,
        DateOnly FechaFin,
        TurnoSabatino Turno);

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
