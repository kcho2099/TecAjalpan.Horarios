using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Oferta;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/oferta-academica")]
[Authorize(Roles = Roles.Administrador + "," + Roles.Jefatura + "," + Roles.Subdireccion)]
public sealed class OfertaAcademicaController(
    ApplicationDbContext dbContext,
    IUsuarioActual usuarioActual) : ControllerBase
{
    private static readonly byte[] OrdenesModulosSabatinos = [1, 2, 3];
    private static readonly int[] SemanasModulosSabatinos = [5, 5, 6];

    [HttpGet("catalogos")]
    public async Task<ActionResult<OfertaCatalogosDto>> Catalogos(
        CancellationToken cancellationToken)
    {
        var periodos = await dbContext.Periodos.AsNoTracking()
            .Where(x => x.Estado != EstadoPeriodo.Cerrado)
            .OrderByDescending(x => x.FechaInicio)
            .Select(x => new OfertaPeriodoDto(
                x.Id, x.Nombre, x.FechaInicio, x.FechaFin, x.SemestresPares,
                x.PermitirExcepcionSemestre, (byte)x.Estado))
            .ToArrayAsync(cancellationToken);
        var carrerasConsulta = dbContext.Carreras.AsNoTracking()
            .Where(x => x.Activo);
        if (!usuarioActual.TieneRol(Roles.Administrador)
            && !usuarioActual.TieneRol(Roles.Subdireccion))
        {
            var idsCarreras = User.FindAll("carrera")
                .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToArray();
            carrerasConsulta = carrerasConsulta.Where(x => idsCarreras.Contains(x.Id));
        }
        var carreras = await carrerasConsulta
            .OrderBy(x => x.Nombre)
            .Select(x => new OfertaCarreraDto(x.Id, x.Clave, x.Nombre))
            .ToArrayAsync(cancellationToken);
        var modalidades = await dbContext.Modalidades.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Tipo)
            .Select(x => new OfertaModalidadDto(x.Id, x.Clave, x.Nombre, (byte)x.Tipo))
            .ToArrayAsync(cancellationToken);
        var idsCarrerasPermitidas = carreras.Select(x => x.Id).ToArray();
        var espacios = await dbContext.Espacios.AsNoTracking()
            .Where(x => x.Activo && idsCarrerasPermitidas.Contains(x.CarreraId))
            .OrderBy(x => x.Carrera.Nombre)
            .ThenBy(x => x.Tipo)
            .ThenBy(x => x.Nombre)
            .Select(x => new OfertaEspacioDto(
                x.Id, x.CarreraId, x.Clave, x.Nombre, x.Tipo, x.Capacidad))
            .ToArrayAsync(cancellationToken);
        return Ok(new OfertaCatalogosDto(periodos, carreras, modalidades, espacios));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PeriodoCarreraOfertaDto>>> Listar(
        [FromQuery] Guid periodoId,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.PeriodosCarreras.AsNoTracking()
            .Where(x => x.PeriodoId == periodoId)
            .Include(x => x.Carrera)
            .Include(x => x.Modalidad)
            .Include(x => x.Grupos)
                .ThenInclude(x => x.Oferta)
                .ThenInclude(x => x.Materia)
            .Include(x => x.Grupos)
                .ThenInclude(x => x.EspacioBase)
            .Include(x => x.Grupos)
                .ThenInclude(x => x.ConfiguracionSabatina)
                .ThenInclude(x => x!.Modulos)
                .ThenInclude(x => x.Materias)
            .AsQueryable();
        if (!usuarioActual.TieneRol(Roles.Administrador)
            && !usuarioActual.TieneRol(Roles.Subdireccion))
        {
            var idsCarreras = User.FindAll("carrera")
                .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToArray();
            consulta = consulta.Where(x => idsCarreras.Contains(x.CarreraId));
        }
        var configuraciones = await consulta.OrderBy(x => x.Carrera.Nombre)
            .ThenBy(x => x.Modalidad.Tipo)
            .ToArrayAsync(cancellationToken);
        return Ok(configuraciones.Select(Mapear).ToArray());
    }

    [HttpGet("materias-disponibles")]
    public async Task<ActionResult<IReadOnlyList<MateriaDisponibleOfertaDto>>> MateriasDisponibles(
        [FromQuery] Guid periodoCarreraId,
        [FromQuery] byte semestre,
        CancellationToken cancellationToken)
    {
        var configuracion = await dbContext.PeriodosCarreras.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == periodoCarreraId, cancellationToken);
        if (configuracion is null)
            return NotFound(new { mensaje = "La carrera y modalidad no están habilitadas en el periodo." });
        if (!usuarioActual.PuedeAccederCarrera(configuracion.CarreraId))
            return Forbid();

        var materias = await dbContext.Materias.AsNoTracking()
            .Where(x => x.Activo
                && x.Semestre == semestre
                && x.Reticula.Activo
                && x.Reticula.CarreraId == configuracion.CarreraId
                && x.MateriasModalidades.Any(m =>
                    m.ModalidadId == configuracion.ModalidadId))
            .OrderBy(x => x.Nombre)
            .Select(x => new MateriaDisponibleOfertaDto(
                x.Id, x.Clave, x.Nombre, x.Semestre,
                x.Creditos, x.HorasSemanales, x.Reticula.Clave))
            .ToArrayAsync(cancellationToken);
        return Ok(materias);
    }

    [HttpPost("configuraciones")]
    public async Task<ActionResult<PeriodoCarreraOfertaDto>> CrearConfiguracion(
        CrearPeriodoCarreraRequest request,
        CancellationToken cancellationToken)
    {
        var periodo = await dbContext.Periodos
            .SingleOrDefaultAsync(x => x.Id == request.PeriodoId, cancellationToken);
        if (periodo is null || periodo.Estado == EstadoPeriodo.Cerrado)
            return BadRequest(new { mensaje = "Selecciona un periodo abierto." });
        if (!await dbContext.Carreras.AnyAsync(
                x => x.Id == request.CarreraId && x.Activo, cancellationToken))
            return BadRequest(new { mensaje = "La carrera no existe o está inactiva." });
        if (!usuarioActual.PuedeAccederCarrera(request.CarreraId))
            return Forbid();
        if (!await dbContext.Modalidades.AnyAsync(
                x => x.Id == request.ModalidadId && x.Activo, cancellationToken))
            return BadRequest(new { mensaje = "La modalidad no existe o está inactiva." });

        var existente = await dbContext.PeriodosCarreras.IgnoreQueryFilters()
            .Include(x => x.Carrera)
            .Include(x => x.Modalidad)
            .Include(x => x.Grupos).ThenInclude(x => x.Oferta).ThenInclude(x => x.Materia)
            .SingleOrDefaultAsync(x => x.PeriodoId == request.PeriodoId
                && x.CarreraId == request.CarreraId
                && x.ModalidadId == request.ModalidadId, cancellationToken);
        if (existente is not null && !existente.Eliminado)
            return Conflict(new { mensaje = "La carrera y modalidad ya están habilitadas en el periodo." });

        PeriodoCarrera configuracion;
        if (existente is not null)
        {
            existente.Eliminado = false;
            existente.UsuarioElimina = null;
            existente.FechaElimina = null;
            configuracion = existente;
        }
        else
        {
            configuracion = new PeriodoCarrera
            {
                PeriodoId = request.PeriodoId,
                CarreraId = request.CarreraId,
                ModalidadId = request.ModalidadId
            };
            dbContext.PeriodosCarreras.Add(configuracion);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await CargarConfiguracion(configuracion, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "La carrera y modalidad ya están habilitadas en el periodo." });
        }
        return Created("api/oferta-academica", Mapear(configuracion));
    }

    [HttpDelete("configuraciones/{id:guid}")]
    public async Task<IActionResult> EliminarConfiguracion(
        Guid id,
        EliminarOfertaRequest request,
        CancellationToken cancellationToken)
    {
        var configuracion = await dbContext.PeriodosCarreras
            .Include(x => x.Periodo)
            .Include(x => x.Grupos)
                .ThenInclude(x => x.Oferta)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (configuracion is null) return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(configuracion.CarreraId)) return Forbid();
        if (!Coincide(request.RowVersion, configuracion.RowVersion))
            return Conflicto("La oferta académica");
        if (configuracion.Periodo.Estado == EstadoPeriodo.Cerrado)
            return Conflict(new { mensaje = "No se puede eliminar la oferta de un periodo cerrado." });

        foreach (var grupo in configuracion.Grupos)
        {
            dbContext.OfertasMaterias.RemoveRange(grupo.Oferta);
            dbContext.Grupos.Remove(grupo);
        }
        dbContext.PeriodosCarreras.Remove(configuracion);

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflicto("La oferta académica"); }
        return NoContent();
    }

    [HttpPost("grupos")]
    public async Task<ActionResult<GrupoOfertaDto>> CrearGrupo(
        GuardarGrupoOfertaRequest request,
        CancellationToken cancellationToken)
    {
        var validacion = await ValidarGrupo(request, null, cancellationToken);
        if (validacion is not null) return BadRequest(new { mensaje = validacion });
        var grupo = new Grupo();
        Aplicar(request, grupo);
        dbContext.Grupos.Add(grupo);
        return await GuardarGrupo(grupo, true, cancellationToken);
    }

    [HttpPut("grupos/{id:guid}")]
    public async Task<ActionResult<GrupoOfertaDto>> ActualizarGrupo(
        Guid id,
        GuardarGrupoOfertaRequest request,
        CancellationToken cancellationToken)
    {
        var grupo = await dbContext.Grupos.Include(x => x.Oferta).ThenInclude(x => x.Materia)
            .Include(x => x.EspacioBase)
            .Include(x => x.PeriodoCarrera)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(grupo.PeriodoCarrera.CarreraId)) return Forbid();
        if (!Coincide(request.RowVersion, grupo.RowVersion)) return Conflicto("El grupo");
        var validacion = await ValidarGrupo(request, id, cancellationToken);
        if (validacion is not null) return BadRequest(new { mensaje = validacion });
        Aplicar(request, grupo);
        return await GuardarGrupo(grupo, false, cancellationToken);
    }

    [HttpDelete("grupos/{id:guid}")]
    public async Task<IActionResult> EliminarGrupo(
        Guid id,
        EliminarOfertaRequest request,
        CancellationToken cancellationToken)
    {
        var grupo = await dbContext.Grupos
            .Include(x => x.PeriodoCarrera)
                .ThenInclude(x => x.Periodo)
            .Include(x => x.Oferta)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (!Coincide(request.RowVersion, grupo.RowVersion))
            return Conflicto("El grupo");
        if (grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
            return Conflict(new { mensaje = "No se puede eliminar un grupo de un periodo cerrado." });

        dbContext.OfertasMaterias.RemoveRange(grupo.Oferta);
        dbContext.Grupos.Remove(grupo);

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflicto("El grupo"); }
        return NoContent();
    }

    [HttpPut("grupos/{id:guid}/materias")]
    public async Task<ActionResult<GrupoOfertaDto>> GuardarMaterias(
        Guid id,
        GuardarMateriasOfertaRequest request,
        CancellationToken cancellationToken)
    {
        var grupo = await dbContext.Grupos
            .Include(x => x.PeriodoCarrera).ThenInclude(x => x.Periodo)
            .Include(x => x.Oferta).ThenInclude(x => x.Materia)
            .Include(x => x.ConfiguracionSabatina)
                .ThenInclude(x => x!.Modulos)
                .ThenInclude(x => x.Materias)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(grupo.PeriodoCarrera.CarreraId)) return Forbid();
        if (!Coincide(request.RowVersionGrupo, grupo.RowVersion)) return Conflicto("El grupo");
        if (grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
            return Conflict(new { mensaje = "No se puede modificar la oferta de un periodo cerrado." });

        var ids = request.MateriaIds.Distinct().ToHashSet();
        var validas = await dbContext.Materias.CountAsync(x =>
            ids.Contains(x.Id)
            && x.Activo
            && x.Semestre == grupo.Semestre
            && x.Reticula.CarreraId == grupo.PeriodoCarrera.CarreraId
            && x.MateriasModalidades.Any(m =>
                m.ModalidadId == grupo.PeriodoCarrera.ModalidadId),
            cancellationToken);
        if (validas != ids.Count)
            return BadRequest(new { mensaje = "Una materia no corresponde a la carrera, modalidad o semestre del grupo." });

        var idsActuales = grupo.Oferta.Where(x => x.Activa)
            .Select(x => x.MateriaId).ToHashSet();
        var cambiaronMaterias = !idsActuales.SetEquals(ids);
        if (cambiaronMaterias
            && await dbContext.CargasAcademicas.AnyAsync(
                x => x.OfertaMateria.GrupoId == id, cancellationToken))
        {
            return Conflict(new { mensaje = "Quita primero las asignaciones docentes del grupo antes de cambiar sus materias." });
        }
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        if (cambiaronMaterias && grupo.ConfiguracionSabatina is not null)
        {
            await dbContext.ConfiguracionesSabatinas
                .Where(x => x.Id == grupo.ConfiguracionSabatina.Id)
                .ExecuteDeleteAsync(cancellationToken);
            dbContext.Entry(grupo.ConfiguracionSabatina).State = EntityState.Detached;
            grupo.ConfiguracionSabatina = null;
        }

        var horasPorMateria = await dbContext.Materias
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.HorasSemanales, cancellationToken);
        var relaciones = await dbContext.OfertasMaterias.IgnoreQueryFilters()
            .Where(x => x.GrupoId == id)
            .ToListAsync(cancellationToken);
        foreach (var relacion in relaciones.Where(x => !x.Eliminado && !ids.Contains(x.MateriaId)))
            dbContext.OfertasMaterias.Remove(relacion);

        foreach (var materiaId in ids)
        {
            var relacion = relaciones.FirstOrDefault(x => x.MateriaId == materiaId);
            var horas = horasPorMateria[materiaId];
            if (relacion is null)
            {
                dbContext.OfertasMaterias.Add(new OfertaMateria
                {
                    GrupoId = id,
                    MateriaId = materiaId,
                    HorasRequeridas = horas,
                    Activa = true
                });
            }
            else if (relacion.Eliminado)
            {
                relacion.Eliminado = false;
                relacion.UsuarioElimina = null;
                relacion.FechaElimina = null;
                relacion.HorasRequeridas = horas;
                relacion.Activa = true;
            }
        }

        dbContext.Entry(grupo).Property(x => x.Nombre).IsModified = true;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await dbContext.Entry(grupo).Collection(x => x.Oferta).Query()
                .Include(x => x.Materia).LoadAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Conflicto("El grupo"); }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "No fue posible sincronizar las materias del grupo." });
        }
        return Ok(Mapear(grupo));
    }

    [HttpPut("grupos/{id:guid}/modulos-sabatinos")]
    public async Task<ActionResult<GrupoOfertaDto>> GuardarModulosSabatinos(
        Guid id,
        GuardarConfiguracionSabatinaRequest request,
        CancellationToken cancellationToken)
    {
        var grupo = await dbContext.Grupos
            .Include(x => x.PeriodoCarrera).ThenInclude(x => x.Periodo)
            .Include(x => x.PeriodoCarrera).ThenInclude(x => x.Modalidad)
            .Include(x => x.EspacioBase)
            .Include(x => x.Oferta).ThenInclude(x => x.Materia)
            .Include(x => x.ConfiguracionSabatina)
                .ThenInclude(x => x!.Modulos)
                .ThenInclude(x => x.Materias)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!usuarioActual.PuedeAccederCarrera(grupo.PeriodoCarrera.CarreraId))
            return Forbid();
        if (!Coincide(request.RowVersionGrupo, grupo.RowVersion))
            return Conflicto("El grupo");
        if (grupo.PeriodoCarrera.Periodo.Estado == EstadoPeriodo.Cerrado)
            return Conflict(new { mensaje = "No se pueden modificar los módulos de un periodo cerrado." });
        if (grupo.PeriodoCarrera.Modalidad.Tipo != TipoModalidad.Sabatina)
            return BadRequest(new { mensaje = "Los módulos sólo aplican a grupos de modalidad sabatina." });

        var modulos = request.Modulos.OrderBy(x => x.Orden).ToArray();
        if (modulos.Length != 3
            || !modulos.Select(x => x.Orden).SequenceEqual(OrdenesModulosSabatinos)
            || !modulos.Select(x => (int)x.Semanas).Order().SequenceEqual(SemanasModulosSabatinos))
        {
            return BadRequest(new { mensaje = "Configura los módulos 1, 2 y 3 con una distribución de 5 + 5 + 6 semanas." });
        }

        var idsMaterias = modulos
            .SelectMany(x => new[] { x.MateriaMatutinaId, x.MateriaVespertinaId })
            .ToArray();
        if (idsMaterias.Any(x => x == Guid.Empty) || idsMaterias.Distinct().Count() != 6)
            return BadRequest(new { mensaje = "Selecciona seis materias distintas: dos para cada módulo." });
        var ofertasActivas = grupo.Oferta.Where(x => x.Activa).ToArray();
        if (ofertasActivas.Length != 6
            || idsMaterias.Any(x => ofertasActivas.All(o => o.Id != x)))
        {
            return BadRequest(new { mensaje = "El grupo sabatino debe tener exactamente seis materias activas y todas deben pertenecer a sus módulos." });
        }

        if (request.FechaInicio.DayOfWeek != DayOfWeek.Saturday)
            return BadRequest(new { mensaje = "La fecha de inicio debe ser un sábado." });
        var primerSabado = request.FechaInicio;
        var periodo = grupo.PeriodoCarrera.Periodo;
        if (primerSabado < periodo.FechaInicio)
            return BadRequest(new { mensaje = "El inicio de los módulos no puede ser anterior al periodo." });
        var ultimoSabado = primerSabado.AddDays((16 - 1) * 7);
        if (ultimoSabado > periodo.FechaFin)
            return BadRequest(new { mensaje = "Los 16 sábados de los módulos exceden la fecha final del periodo." });

        var tieneCarga = await dbContext.CargasAcademicas.AnyAsync(x =>
            x.OfertaMateria.GrupoId == id, cancellationToken);
        if (tieneCarga)
            return Conflict(new { mensaje = "Quita primero las asignaciones docentes del grupo antes de cambiar sus módulos." });

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var configuracion = grupo.ConfiguracionSabatina;
        if (configuracion is null)
        {
            configuracion = new ConfiguracionSabatina { GrupoId = grupo.Id };
            dbContext.ConfiguracionesSabatinas.Add(configuracion);
        }
        else
        {
            var materiasAnteriores = configuracion.Modulos
                .SelectMany(x => x.Materias).ToArray();
            await dbContext.ModulosMaterias
                .Where(x => x.ModuloSabatino.ConfiguracionSabatinaId == configuracion.Id)
                .ExecuteDeleteAsync(cancellationToken);
            foreach (var materiaAnterior in materiasAnteriores)
                dbContext.Entry(materiaAnterior).State = EntityState.Detached;
            foreach (var moduloExistente in configuracion.Modulos)
                moduloExistente.Materias.Clear();
        }
        configuracion.FechaInicio = primerSabado;
        configuracion.Validada = false;
        var fechaModulo = primerSabado;
        foreach (var moduloRequest in modulos)
        {
            var modulo = configuracion.Modulos.SingleOrDefault(
                x => x.Orden == moduloRequest.Orden);
            if (modulo is null)
            {
                modulo = new ModuloSabatino { Orden = moduloRequest.Orden };
                configuracion.Modulos.Add(modulo);
            }
            modulo.Semanas = moduloRequest.Semanas;
            modulo.FechaInicio = fechaModulo;
            modulo.FechaFin = fechaModulo.AddDays((moduloRequest.Semanas - 1) * 7);
            modulo.Materias.Add(new ModuloMateria
            {
                Turno = TurnoSabatino.Matutino,
                OfertaMateriaId = moduloRequest.MateriaMatutinaId
            });
            modulo.Materias.Add(new ModuloMateria
            {
                Turno = TurnoSabatino.Vespertino,
                OfertaMateriaId = moduloRequest.MateriaVespertinaId
            });
            fechaModulo = modulo.FechaFin.AddDays(7);
        }
        configuracion.Validar();
        dbContext.Entry(grupo).Property(x => x.Nombre).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Conflicto("El grupo"); }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "No fue posible guardar los módulos sabatinos. Recarga e inténtalo nuevamente." });
        }

        grupo.ConfiguracionSabatina = configuracion;
        return Ok(Mapear(grupo));
    }

    private async Task<string?> ValidarGrupo(
        GuardarGrupoOfertaRequest request,
        Guid? grupoId,
        CancellationToken cancellationToken)
    {
        var configuracion = await dbContext.PeriodosCarreras
            .Include(x => x.Periodo)
            .SingleOrDefaultAsync(x => x.Id == request.PeriodoCarreraId, cancellationToken);
        if (configuracion is null) return "La carrera y modalidad no están habilitadas en el periodo.";
        if (!usuarioActual.PuedeAccederCarrera(configuracion.CarreraId))
            return "No tienes permiso para configurar la oferta de esa carrera.";
        if (configuracion.Periodo.Estado == EstadoPeriodo.Cerrado)
            return "No se pueden modificar grupos de un periodo cerrado.";
        var semestre = checked((byte)request.Semestre);
        var paridadCorrecta = configuracion.Periodo.SemestresPares
            ? semestre % 2 == 0
            : semestre % 2 != 0;
        if (!paridadCorrecta && !configuracion.Periodo.PermitirExcepcionSemestre)
            return $"El periodo sólo admite semestres {(configuracion.Periodo.SemestresPares ? "pares" : "impares")}.";
        var clave = request.Clave.Trim().ToUpperInvariant();
        if (await dbContext.Grupos.AnyAsync(x =>
                x.PeriodoCarreraId == request.PeriodoCarreraId
                && x.Clave == clave
                && (!grupoId.HasValue || x.Id != grupoId.Value), cancellationToken))
            return "Ya existe un grupo con esa clave en la carrera y modalidad.";
        if (request.EspacioBaseId == Guid.Empty)
            return "Selecciona el aula o laboratorio base del grupo.";
        var espacioValido = await dbContext.Espacios.AnyAsync(x =>
            x.Id == request.EspacioBaseId
            && x.Activo
            && x.CarreraId == configuracion.CarreraId,
            cancellationToken);
        if (!espacioValido)
            return "El espacio debe estar activo y pertenecer a la misma carrera del grupo.";
        var espacioOcupado = await dbContext.Grupos.AnyAsync(x =>
            x.EspacioBaseId == request.EspacioBaseId
            && x.PeriodoCarrera.PeriodoId == configuracion.PeriodoId
            && x.PeriodoCarrera.ModalidadId == configuracion.ModalidadId
            && (!grupoId.HasValue || x.Id != grupoId.Value),
            cancellationToken);
        if (espacioOcupado)
            return "El aula o laboratorio ya está asignado a otro grupo de la misma modalidad en este periodo.";
        return null;
    }

    private async Task<ActionResult<GrupoOfertaDto>> GuardarGrupo(
        Grupo grupo, bool creado, CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflicto("El grupo"); }
        catch (DbUpdateException) { return Conflict(new { mensaje = "Ya existe esa clave de grupo." }); }
        var espacio = await dbContext.Espacios.AsNoTracking()
            .SingleAsync(x => x.Id == grupo.EspacioBaseId, cancellationToken);
        var resultado = Mapear(grupo, espacio);
        return creado ? Created("api/oferta-academica/grupos", resultado) : Ok(resultado);
    }

    private async Task CargarConfiguracion(
        PeriodoCarrera configuracion,
        CancellationToken cancellationToken)
    {
        await dbContext.Entry(configuracion).Reference(x => x.Carrera).LoadAsync(cancellationToken);
        await dbContext.Entry(configuracion).Reference(x => x.Modalidad).LoadAsync(cancellationToken);
        await dbContext.Entry(configuracion).Collection(x => x.Grupos).Query()
            .Include(x => x.Oferta).ThenInclude(x => x.Materia)
            .LoadAsync(cancellationToken);
        await dbContext.Entry(configuracion).Collection(x => x.Grupos).Query()
            .Include(x => x.EspacioBase)
            .LoadAsync(cancellationToken);
        await dbContext.Entry(configuracion).Collection(x => x.Grupos).Query()
            .Include(x => x.ConfiguracionSabatina)
                .ThenInclude(x => x!.Modulos)
                .ThenInclude(x => x.Materias)
            .LoadAsync(cancellationToken);
    }

    private static void Aplicar(GuardarGrupoOfertaRequest request, Grupo grupo)
    {
        grupo.PeriodoCarreraId = request.PeriodoCarreraId;
        grupo.Semestre = checked((byte)request.Semestre);
        grupo.Clave = request.Clave.Trim().ToUpperInvariant();
        grupo.Nombre = request.Nombre.Trim();
        grupo.EspacioBaseId = request.EspacioBaseId;
    }

    private static PeriodoCarreraOfertaDto Mapear(PeriodoCarrera x) => new(
        x.Id, x.PeriodoId, x.CarreraId, x.Carrera.Clave, x.Carrera.Nombre,
        x.ModalidadId, x.Modalidad.Nombre,
        x.Grupos.Where(g => !g.Eliminado)
            .OrderBy(g => g.Semestre)
            .ThenBy(g => g.Clave)
            .Select(Mapear)
            .ToArray(),
        Convert.ToBase64String(x.RowVersion));

    private static GrupoOfertaDto Mapear(Grupo x) => Mapear(x, x.EspacioBase);

    private static GrupoOfertaDto Mapear(Grupo x, Espacio? espacio) => new(
        x.Id, x.Semestre, x.Clave, x.Nombre,
        x.EspacioBaseId,
        espacio?.Clave ?? x.EspacioBase?.Clave,
        espacio?.Nombre ?? x.EspacioBase?.Nombre,
        espacio?.Tipo ?? x.EspacioBase?.Tipo,
        x.Oferta.Where(o => !o.Eliminado && o.Activa).OrderBy(o => o.Materia.Nombre)
            .Select(o =>
            {
                var moduloMateria = x.ConfiguracionSabatina?.Modulos
                    .SelectMany(m => m.Materias.Select(mm => new { Modulo = m, Materia = mm }))
                    .SingleOrDefault(mm => mm.Materia.OfertaMateriaId == o.Id);
                return new MateriaOfertaDto(
                    o.Id, o.MateriaId, o.Materia.Clave, o.Materia.Nombre,
                    o.Materia.Creditos, o.HorasRequeridas, o.Activa,
                    moduloMateria?.Modulo.Orden,
                    moduloMateria?.Modulo.Semanas,
                    moduloMateria?.Modulo.FechaInicio,
                    moduloMateria?.Modulo.FechaFin,
                    moduloMateria is null ? null : (byte)moduloMateria.Materia.Turno,
                    moduloMateria?.Materia.Turno == TurnoSabatino.Matutino
                        ? "08:00–12:00"
                        : moduloMateria?.Materia.Turno == TurnoSabatino.Vespertino
                            ? "12:00–16:00"
                            : null);
            }).ToArray(),
        x.ConfiguracionSabatina is null
            ? null
            : new ConfiguracionSabatinaOfertaDto(
                x.ConfiguracionSabatina.Id,
                x.ConfiguracionSabatina.FechaInicio,
                x.ConfiguracionSabatina.Validada,
                x.ConfiguracionSabatina.Modulos.OrderBy(m => m.Orden)
                    .Select(m => new ModuloSabatinoOfertaDto(
                        m.Id, m.Orden, m.Semanas, m.FechaInicio, m.FechaFin,
                        m.Materias.Single(mm => mm.Turno == TurnoSabatino.Matutino).OfertaMateriaId,
                        m.Materias.Single(mm => mm.Turno == TurnoSabatino.Vespertino).OfertaMateriaId))
                    .ToArray()),
        Convert.ToBase64String(x.RowVersion));

    private ConflictObjectResult Conflicto(string entidad) =>
        Conflict(new { mensaje = $"{entidad} fue modificado por otra persona. Recarga e inténtalo nuevamente." });

    private static bool Coincide(string? valor, byte[] actual)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        try { return Convert.FromBase64String(valor).SequenceEqual(actual); }
        catch (FormatException) { return false; }
    }
}
