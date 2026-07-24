using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Auditoria;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/bitacora")]
[Authorize(Roles = Roles.Administrador)]
public sealed class BitacoraController(ApplicationDbContext dbContext) : ControllerBase
{
    private const int MaximoRegistros = 500;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BitacoraDto>>> Listar(
        CancellationToken cancellationToken)
    {
        var registros = await dbContext.Bitacora
            .AsNoTracking()
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .Take(MaximoRegistros)
            .ToArrayAsync(cancellationToken);

        var idsUsuarios = registros
            .Select(x => x.UsuarioId)
            .Where(x => x != "sistema")
            .Distinct()
            .ToArray();

        var usuarios = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(x => idsUsuarios.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.NombreCompleto)
                    ? x.Email ?? x.Id
                    : x.NombreCompleto,
                cancellationToken);

        return Ok(registros.Select(x => new BitacoraDto(
            x.Id,
            x.Entidad,
            x.RegistroId,
            x.Accion,
            x.UsuarioId == "sistema"
                ? "Sistema"
                : usuarios.GetValueOrDefault(x.UsuarioId, x.UsuarioId),
            x.Fecha,
            x.ValoresAnteriores,
            x.ValoresNuevos,
            x.CorrelationId)).ToArray());
    }
}
