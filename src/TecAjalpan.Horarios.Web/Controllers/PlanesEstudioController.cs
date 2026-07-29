using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.PlanesEstudio;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/planes-estudio")]
[Authorize(Roles = "Administrador,Secretaría,Jefatura,Subdirección Académica")]
public sealed class PlanesEstudioController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("modalidades")]
    public async Task<ActionResult<IReadOnlyList<ModalidadPlanDto>>> ListarModalidades(
        CancellationToken cancellationToken) =>
        Ok(await dbContext.Modalidades.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => new ModalidadPlanDto(x.Id, x.Clave, x.Nombre))
            .ToArrayAsync(cancellationToken));

    [HttpGet("reticulas")]
    public async Task<ActionResult<IReadOnlyList<ReticulaDto>>> ListarReticulas(
        [FromQuery] Guid carreraId,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Reticulas.AsNoTracking()
            .Where(x => x.CarreraId == carreraId);
        if (!User.IsInRole(Roles.Administrador))
            consulta = consulta.Where(x => x.Activo);
        return Ok((await consulta.OrderByDescending(x => x.Activo)
            .ThenByDescending(x => x.InicioVigencia)
            .ToArrayAsync(cancellationToken)).Select(Mapear).ToArray());
    }

    [HttpGet("materias")]
    public async Task<ActionResult<IReadOnlyList<MateriaDto>>> ListarMaterias(
        [FromQuery] Guid reticulaId,
        [FromQuery] byte? semestre,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Materias.AsNoTracking()
            .Include(x => x.Modalidades).ThenInclude(x => x.Modalidad)
            .Where(x => x.ReticulaId == reticulaId);
        if (semestre.HasValue)
            consulta = consulta.Where(x => x.Semestre == semestre);
        if (!User.IsInRole(Roles.Administrador))
            consulta = consulta.Where(x => x.Activo);
        return Ok((await consulta.OrderBy(x => x.Semestre).ThenBy(x => x.Nombre)
            .ToArrayAsync(cancellationToken)).Select(Mapear).ToArray());
    }

    [HttpPost("reticulas")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ReticulaDto>> CrearReticula(
        GuardarReticulaRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Carreras.AnyAsync(x => x.Id == request.CarreraId && x.Activo, cancellationToken))
            return BadRequest(new { mensaje = "La carrera no existe o está inactiva." });
        var reticula = new Reticula();
        Aplicar(request, reticula);
        dbContext.Reticulas.Add(reticula);
        return await GuardarReticula(reticula, true, cancellationToken);
    }

    [HttpPut("reticulas/{id:guid}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ReticulaDto>> ActualizarReticula(
        Guid id, GuardarReticulaRequest request, CancellationToken cancellationToken)
    {
        var reticula = await dbContext.Reticulas.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reticula is null) return NotFound();
        if (!Coincide(request.RowVersion, reticula.RowVersion)) return Conflicto("La retícula");
        Aplicar(request, reticula);
        return await GuardarReticula(reticula, false, cancellationToken);
    }

    [HttpPatch("reticulas/{id:guid}/estado")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ReticulaDto>> EstadoReticula(
        Guid id, CambiarEstadoPlanRequest request, CancellationToken cancellationToken)
    {
        var reticula = await dbContext.Reticulas.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reticula is null) return NotFound();
        if (!Coincide(request.RowVersion, reticula.RowVersion)) return Conflicto("La retícula");
        reticula.Activo = request.Activo;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Mapear(reticula));
    }

    [HttpPost("materias")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MateriaDto>> CrearMateria(
        GuardarMateriaRequest request, CancellationToken cancellationToken)
    {
        var error = await ValidarMateria(request, cancellationToken);
        if (error is not null) return BadRequest(new { mensaje = error });
        var materia = new Materia();
        Aplicar(request, materia);
        dbContext.Materias.Add(materia);
        await SincronizarModalidades(materia, request.ModalidadIds, cancellationToken);
        return await GuardarMateria(materia, true, cancellationToken);
    }

    [HttpPut("materias/{id:guid}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MateriaDto>> ActualizarMateria(
        Guid id, GuardarMateriaRequest request, CancellationToken cancellationToken)
    {
        var error = await ValidarMateria(request, cancellationToken);
        if (error is not null) return BadRequest(new { mensaje = error });
        var materia = await dbContext.Materias.Include(x => x.Modalidades)
            .ThenInclude(x => x.Modalidad).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (materia is null) return NotFound();
        if (!Coincide(request.RowVersion, materia.RowVersion)) return Conflicto("La materia");
        Aplicar(request, materia);
        await SincronizarModalidades(materia, request.ModalidadIds, cancellationToken);
        return await GuardarMateria(materia, false, cancellationToken);
    }

    [HttpPatch("materias/{id:guid}/estado")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MateriaDto>> EstadoMateria(
        Guid id, CambiarEstadoPlanRequest request, CancellationToken cancellationToken)
    {
        var materia = await dbContext.Materias.Include(x => x.Modalidades)
            .ThenInclude(x => x.Modalidad).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (materia is null) return NotFound();
        if (!Coincide(request.RowVersion, materia.RowVersion)) return Conflicto("La materia");
        materia.Activo = request.Activo;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Mapear(materia));
    }

    private async Task SincronizarModalidades(
        Materia materia, IEnumerable<Guid> modalidadIds, CancellationToken cancellationToken)
    {
        var ids = modalidadIds.Distinct().ToHashSet();
        foreach (var actual in materia.Modalidades.Where(x => !ids.Contains(x.ModalidadId)).ToArray())
            dbContext.MateriasModalidades.Remove(actual);
        var actuales = materia.Modalidades.Select(x => x.ModalidadId).ToHashSet();
        foreach (var id in ids.Where(x => !actuales.Contains(x)))
            materia.Modalidades.Add(new MateriaModalidad { ModalidadId = id });
    }

    private async Task<string?> ValidarMateria(
        GuardarMateriaRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Reticulas.AnyAsync(
                x => x.Id == request.ReticulaId && x.Activo, cancellationToken))
            return "La retícula no existe o está inactiva.";
        var ids = request.ModalidadIds.Distinct().ToArray();
        if (ids.Length == 0) return "Selecciona al menos una modalidad.";
        var validas = await dbContext.Modalidades.CountAsync(
            x => ids.Contains(x.Id) && x.Activo, cancellationToken);
        return validas == ids.Length ? null : "Una modalidad no existe o está inactiva.";
    }

    private async Task<ActionResult<ReticulaDto>> GuardarReticula(
        Reticula reticula, bool creada, CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflicto("La retícula"); }
        catch (DbUpdateException) { return Conflict(new { mensaje = "Ya existe esa clave de retícula en la carrera." }); }
        return creada ? Created("api/planes-estudio/reticulas", Mapear(reticula)) : Ok(Mapear(reticula));
    }

    private async Task<ActionResult<MateriaDto>> GuardarMateria(
        Materia materia, bool creada, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContext.Entry(materia).Collection(x => x.Modalidades).Query()
                .Include(x => x.Modalidad).LoadAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Conflicto("La materia"); }
        catch (DbUpdateException) { return Conflict(new { mensaje = "Ya existe esa clave de materia en la retícula." }); }
        return creada ? Created("api/planes-estudio/materias", Mapear(materia)) : Ok(Mapear(materia));
    }

    private static void Aplicar(GuardarReticulaRequest r, Reticula e)
    {
        e.CarreraId = r.CarreraId; e.Clave = r.Clave.Trim().ToUpperInvariant();
        e.Nombre = r.Nombre.Trim(); e.InicioVigencia = r.InicioVigencia; e.FinVigencia = r.FinVigencia;
    }

    private static void Aplicar(GuardarMateriaRequest r, Materia e)
    {
        e.ReticulaId = r.ReticulaId; e.Clave = r.Clave.Trim().ToUpperInvariant();
        e.Nombre = r.Nombre.Trim(); e.Semestre = checked((byte)r.Semestre);
        e.Creditos = checked((byte)r.Creditos);
        e.HorasTeoricas = checked((byte)r.HorasTeoricas);
        e.HorasPracticas = checked((byte)r.HorasPracticas);
        e.HorasSemanales = checked((byte)(r.HorasTeoricas + r.HorasPracticas));
    }

    private static ReticulaDto Mapear(Reticula x) => new(
        x.Id, x.CarreraId, x.Clave, x.Nombre, x.InicioVigencia, x.FinVigencia,
        x.Activo, Convert.ToBase64String(x.RowVersion));

    private static MateriaDto Mapear(Materia x) => new(
        x.Id, x.ReticulaId, x.Clave, x.Nombre, x.Semestre, x.Creditos,
        x.HorasTeoricas, x.HorasPracticas, x.HorasSemanales, x.Activo,
        x.Modalidades.Where(m => !m.Eliminado).OrderBy(m => m.Modalidad.Nombre)
            .Select(m => new ModalidadPlanDto(m.ModalidadId, m.Modalidad.Clave, m.Modalidad.Nombre)).ToArray(),
        Convert.ToBase64String(x.RowVersion));

    private ConflictObjectResult Conflicto(string entidad) =>
        Conflict(new { mensaje = $"{entidad} fue modificada por otra persona. Recarga e inténtalo nuevamente." });

    private static bool Coincide(string? valor, byte[] actual)
    {
        try { return !string.IsNullOrWhiteSpace(valor) && Convert.FromBase64String(valor).SequenceEqual(actual); }
        catch (FormatException) { return false; }
    }
}
