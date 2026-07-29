using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Periodos;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/periodos")]
[Authorize(Policy = Politicas.AdministrarPeriodos)]
public sealed class PeriodosController(
    IPeriodoRepository repository,
    ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PeriodoDto>>> Listar(
        CancellationToken cancellationToken)
    {
        await CerrarPeriodosVencidosAsync(cancellationToken);
        var periodos = await repository.ListarAsync(cancellationToken);
        return Ok(periodos.Select(Mapear).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PeriodoDto>> Obtener(
        Guid id,
        CancellationToken cancellationToken)
    {
        await CerrarPeriodosVencidosAsync(cancellationToken);
        var periodo = await repository.ObtenerAsync(id, cancellationToken);
        return periodo is null ? NotFound() : Ok(Mapear(periodo));
    }

    [HttpPost]
    public async Task<ActionResult<PeriodoDto>> Crear(
        GuardarPeriodoRequest request,
        CancellationToken cancellationToken)
    {
        await CerrarPeriodosVencidosAsync(cancellationToken);

        var errorEstado = await ValidarActivacionAsync(
            request.Estado,
            null,
            request.FechaFin,
            cancellationToken);
        if (errorEstado is not null)
        {
            return Conflict(new { mensaje = errorEstado });
        }

        if (!EsAdministrador() && request.Semanas != 16)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                mensaje = "Los periodos ordinarios deben tener 16 semanas. Sólo un administrador puede autorizar otra duración."
            });
        }

        var periodo = new Periodo();
        Aplicar(request, periodo);
        await repository.AgregarAsync(periodo, cancellationToken);

        try
        {
            await repository.GuardarCambiosAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var mensaje = request.Estado == (byte)EstadoPeriodo.Activo
                ? "Ya existe un periodo activo. Ciérralo antes de activar otro."
                : "Ya existe un periodo con ese nombre.";
            return Conflict(new { mensaje });
        }

        return CreatedAtAction(nameof(Obtener), new { id = periodo.Id }, Mapear(periodo));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PeriodoDto>> Actualizar(
        Guid id,
        GuardarPeriodoRequest request,
        CancellationToken cancellationToken)
    {
        await CerrarPeriodosVencidosAsync(cancellationToken);
        var periodo = await repository.ObtenerAsync(id, cancellationToken);
        if (periodo is null)
        {
            return NotFound();
        }

        if (periodo.Estado == EstadoPeriodo.Cerrado)
        {
            return Conflict(new
            {
                mensaje = "El periodo está cerrado y no puede editarse. Un administrador debe reabrirlo antes de modificar sus datos."
            });
        }

        if (!CoincideRowVersion(request.RowVersion, periodo.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "El periodo fue modificado por otra persona. Recarga los datos e inténtalo nuevamente."
            });
        }

        if (!EsAdministrador()
            && request.Semanas != 16
            && request.Semanas != periodo.Semanas)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                mensaje = "Sólo un administrador puede cambiar la duración del periodo a un valor distinto de 16 semanas."
            });
        }

        var errorEstado = await ValidarActivacionAsync(
            request.Estado,
            periodo.Id,
            request.FechaFin,
            cancellationToken);
        if (errorEstado is not null)
        {
            return Conflict(new { mensaje = errorEstado });
        }

        Aplicar(request, periodo);

        try
        {
            await repository.GuardarCambiosAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "El periodo cambió mientras lo editabas. Recarga los datos e inténtalo nuevamente."
            });
        }
        catch (DbUpdateException)
        {
            var mensaje = request.Estado == (byte)EstadoPeriodo.Activo
                ? "Ya existe un periodo activo. Ciérralo antes de activar otro."
                : "Ya existe un periodo con ese nombre.";
            return Conflict(new { mensaje });
        }

        return Ok(Mapear(periodo));
    }

    [HttpPost("{id:guid}/reabrir")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<PeriodoDto>> Reabrir(
        Guid id,
        ReabrirPeriodoRequest request,
        CancellationToken cancellationToken)
    {
        var periodo = await repository.ObtenerAsync(id, cancellationToken);
        if (periodo is null)
        {
            return NotFound();
        }

        if (periodo.Estado != EstadoPeriodo.Cerrado)
        {
            return Conflict(new
            {
                mensaje = "El periodo ya está abierto."
            });
        }

        if (!CoincideRowVersion(request.RowVersion, periodo.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "El periodo fue modificado por otra persona. Recarga los datos e inténtalo nuevamente."
            });
        }

        periodo.Estado = EstadoPeriodo.Configuracion;

        try
        {
            await repository.GuardarCambiosAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                mensaje = "El periodo cambió mientras intentabas reabrirlo. Recarga los datos e inténtalo nuevamente."
            });
        }

        return Ok(Mapear(periodo));
    }

    private async Task CerrarPeriodosVencidosAsync(
        CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var vencidos = await dbContext.Periodos
            .Where(x => !x.Eliminado
                && x.Estado == EstadoPeriodo.Activo
                && x.FechaFin < hoy)
            .ToArrayAsync(cancellationToken);
        if (vencidos.Length == 0)
        {
            return;
        }

        foreach (var vencido in vencidos)
        {
            vencido.Estado = EstadoPeriodo.Cerrado;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> ValidarActivacionAsync(
        byte estado,
        Guid? periodoId,
        DateOnly fechaFin,
        CancellationToken cancellationToken)
    {
        if (estado != (byte)EstadoPeriodo.Activo)
        {
            return null;
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        if (fechaFin < hoy)
        {
            return "No se puede activar un periodo cuya fecha de fin ya terminó.";
        }

        var existeOtroActivo = await dbContext.Periodos
            .AsNoTracking()
            .AnyAsync(x => !x.Eliminado
                && x.Estado == EstadoPeriodo.Activo
                && (!periodoId.HasValue || x.Id != periodoId.Value),
                cancellationToken);
        return existeOtroActivo
            ? "Ya existe un periodo activo. Ciérralo antes de activar otro."
            : null;
    }

    private static void Aplicar(GuardarPeriodoRequest request, Periodo periodo)
    {
        periodo.Nombre = request.Nombre.Trim();
        periodo.FechaInicio = request.FechaInicio;
        periodo.FechaFin = request.FechaFin;
        periodo.Semanas = checked((byte)request.Semanas);
        periodo.SemestresPares = request.SemestresPares;
        periodo.PermitirExcepcionSemestre = request.PermitirExcepcionSemestre;
        periodo.Estado = (EstadoPeriodo)request.Estado;
    }

    private static PeriodoDto Mapear(Periodo periodo) =>
        new(
            periodo.Id,
            periodo.Nombre,
            periodo.FechaInicio,
            periodo.FechaFin,
            periodo.Semanas,
            periodo.SemestresPares,
            periodo.PermitirExcepcionSemestre,
            (byte)periodo.Estado,
            periodo.Estado switch
            {
                EstadoPeriodo.Configuracion => "Configuración",
                EstadoPeriodo.Activo => "Activo",
                EstadoPeriodo.Cerrado => "Cerrado",
                _ => "Desconocido"
            },
            Convert.ToBase64String(periodo.RowVersion));

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

    private bool EsAdministrador() => User.IsInRole(Roles.Administrador);
}
