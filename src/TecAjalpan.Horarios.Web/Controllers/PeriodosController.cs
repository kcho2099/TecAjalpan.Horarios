using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Contracts.Periodos;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/periodos")]
public sealed class PeriodosController(IPeriodoRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PeriodoDto>>> Listar(
        CancellationToken cancellationToken)
    {
        var periodos = await repository.ListarAsync(cancellationToken);
        return Ok(periodos.Select(Mapear).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PeriodoDto>> Obtener(
        Guid id,
        CancellationToken cancellationToken)
    {
        var periodo = await repository.ObtenerAsync(id, cancellationToken);
        return periodo is null ? NotFound() : Ok(Mapear(periodo));
    }

    [HttpPost]
    public async Task<ActionResult<PeriodoDto>> Crear(
        GuardarPeriodoRequest request,
        CancellationToken cancellationToken)
    {
        var periodo = new Periodo();
        Aplicar(request, periodo);
        await repository.AgregarAsync(periodo, cancellationToken);

        try
        {
            await repository.GuardarCambiosAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "Ya existe un periodo con ese nombre." });
        }

        return CreatedAtAction(nameof(Obtener), new { id = periodo.Id }, Mapear(periodo));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PeriodoDto>> Actualizar(
        Guid id,
        GuardarPeriodoRequest request,
        CancellationToken cancellationToken)
    {
        var periodo = await repository.ObtenerAsync(id, cancellationToken);
        if (periodo is null)
        {
            return NotFound();
        }

        if (!CoincideRowVersion(request.RowVersion, periodo.RowVersion))
        {
            return Conflict(new
            {
                mensaje = "El periodo fue modificado por otra persona. Recarga los datos e inténtalo nuevamente."
            });
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
            return Conflict(new { mensaje = "Ya existe un periodo con ese nombre." });
        }

        return Ok(Mapear(periodo));
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
}
