using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Carreras;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/carreras")]
[Authorize(Roles = "Administrador,Secretaría,Jefatura,Subdirección Académica")]
public sealed class CarrerasController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CarreraDto>>> Listar(
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Carreras.AsNoTracking();
        if (!User.IsInRole(Roles.Administrador))
        {
            consulta = consulta.Where(x => x.Activo);
        }

        var carreras = await consulta
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.Nombre)
            .ToArrayAsync(cancellationToken);

        return Ok(carreras.Select(Mapear).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<CarreraDto>> Crear(
        GuardarCarreraRequest request,
        CancellationToken cancellationToken)
    {
        var carrera = new Carrera();
        Aplicar(request, carrera);
        dbContext.Carreras.Add(carrera);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "Ya existe una carrera con esa clave." });
        }

        return CreatedAtAction(nameof(Listar), Mapear(carrera));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<CarreraDto>> Actualizar(
        Guid id,
        GuardarCarreraRequest request,
        CancellationToken cancellationToken)
    {
        var carrera = await dbContext.Carreras
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (carrera is null)
        {
            return NotFound();
        }

        if (!CoincideRowVersion(request.RowVersion, carrera.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "La carrera fue modificada por otra persona. Recarga los datos e inténtalo nuevamente."
            });
        }

        Aplicar(request, carrera);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "La carrera cambió mientras la editabas. Recarga los datos e inténtalo nuevamente."
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "Ya existe una carrera con esa clave." });
        }

        return Ok(Mapear(carrera));
    }

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<CarreraDto>> CambiarEstado(
        Guid id,
        CambiarEstadoCarreraRequest request,
        CancellationToken cancellationToken)
    {
        var carrera = await dbContext.Carreras
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (carrera is null)
        {
            return NotFound();
        }

        if (!CoincideRowVersion(request.RowVersion, carrera.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "La carrera fue modificada por otra persona. Recarga los datos e inténtalo nuevamente."
            });
        }

        if (carrera.Activo == request.Activo)
        {
            return Ok(Mapear(carrera));
        }

        carrera.Activo = request.Activo;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "La carrera cambió mientras actualizabas su estado. Recarga los datos e inténtalo nuevamente."
            });
        }

        return Ok(Mapear(carrera));
    }

    private static void Aplicar(GuardarCarreraRequest request, Carrera carrera)
    {
        carrera.Clave = request.Clave.Trim().ToUpperInvariant();
        carrera.Nombre = request.Nombre.Trim();
    }

    private static CarreraDto Mapear(Carrera carrera) =>
        new(
            carrera.Id,
            carrera.Clave,
            carrera.Nombre,
            carrera.Activo,
            Convert.ToBase64String(carrera.RowVersion));

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
