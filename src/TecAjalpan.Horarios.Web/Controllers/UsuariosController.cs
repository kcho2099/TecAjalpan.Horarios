using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Contracts.Usuarios;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Infrastructure.Identity;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.AdministrarUsuarios)]
public sealed class UsuariosController(
    UserManager<UsuarioAplicacion> userManager,
    ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(
        CancellationToken cancellationToken)
    {
        var usuarios = await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.NombreCompleto)
            .ToListAsync(cancellationToken);

        var resultado = new List<UsuarioDto>(usuarios.Count);
        foreach (var usuario in usuarios)
        {
            resultado.Add(await MapearAsync(usuario, cancellationToken));
        }

        return Ok(resultado);
    }

    [HttpGet("catalogos")]
    public async Task<ActionResult<CatalogosUsuarioDto>> ObtenerCatalogos(
        CancellationToken cancellationToken)
    {
        var carreras = await dbContext.Carreras
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => new CarreraUsuarioDto(x.Id, x.Clave, x.Nombre))
            .ToArrayAsync(cancellationToken);

        return Ok(new CatalogosUsuarioDto(Roles.Todos, carreras));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ResultadoUsuarioDto>> Crear(
        GuardarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var validacion = await ValidarRequestAsync(request, null, cancellationToken);
        if (validacion is not null)
        {
            return BadRequest(new { mensaje = validacion });
        }

        var contrasenaTemporal = GenerarContrasenaTemporal();
        var usuario = new UsuarioAplicacion
        {
            UserName = request.Correo.Trim(),
            Email = request.Correo.Trim(),
            EmailConfirmed = true,
            NombreCompleto = request.Nombre.Trim(),
            DebeCambiarContrasena = true,
            Activo = true
        };

        await using var transaccion =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var creacion = await userManager.CreateAsync(usuario, contrasenaTemporal);
        if (!creacion.Succeeded)
        {
            return BadRequest(new { mensaje = MensajeErrores(creacion) });
        }

        var asignacionRol = await userManager.AddToRoleAsync(usuario, request.Rol);
        if (!asignacionRol.Succeeded)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return BadRequest(new { mensaje = MensajeErrores(asignacionRol) });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(Listar),
            new ResultadoUsuarioDto(
                await MapearAsync(usuario, cancellationToken),
                contrasenaTemporal,
                "Usuario creado correctamente."));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ResultadoUsuarioDto>> Actualizar(
        string id,
        GuardarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await ObtenerSinFiltroAsync(id, cancellationToken);
        if (usuario is null)
        {
            return NotFound();
        }

        var validacion = await ValidarRequestAsync(request, id, cancellationToken);
        if (validacion is not null)
        {
            return BadRequest(new { mensaje = validacion });
        }

        var rolesActuales = await userManager.GetRolesAsync(usuario);
        if (usuario.Id == userManager.GetUserId(User)
            && !rolesActuales.Contains(request.Rol))
        {
            return BadRequest(new
            {
                mensaje = "No puedes cambiar el rol de tu propia cuenta."
            });
        }

        if (rolesActuales.Contains(Roles.Administrador)
            && request.Rol != Roles.Administrador
            && await EsUltimoAdministradorActivoAsync(usuario.Id, cancellationToken))
        {
            return BadRequest(new
            {
                mensaje = "No puedes retirar el rol al único administrador activo."
            });
        }

        if (request.Rol == Roles.Jefatura
            && await dbContext.UsuariosCarreras.CountAsync(
                x => x.UsuarioId == usuario.Id,
                cancellationToken) > 1)
        {
            return BadRequest(new
            {
                mensaje = "Antes de asignar el rol Jefatura, deja al usuario con una sola carrera."
            });
        }

        await using var transaccion =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        usuario.NombreCompleto = request.Nombre.Trim();
        usuario.Email = request.Correo.Trim();
        usuario.UserName = request.Correo.Trim();
        usuario.NormalizedEmail = userManager.NormalizeEmail(usuario.Email);
        usuario.NormalizedUserName = userManager.NormalizeName(usuario.UserName);

        var actualizacion = await userManager.UpdateAsync(usuario);
        if (!actualizacion.Succeeded)
        {
            return BadRequest(new { mensaje = MensajeErrores(actualizacion) });
        }

        if (rolesActuales.Count > 0)
        {
            var quitarRoles = await userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            if (!quitarRoles.Succeeded)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return BadRequest(new { mensaje = MensajeErrores(quitarRoles) });
            }
        }

        var agregarRol = await userManager.AddToRoleAsync(usuario, request.Rol);
        if (!agregarRol.Succeeded)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return BadRequest(new { mensaje = MensajeErrores(agregarRol) });
        }

        if (request.Rol is Roles.Administrador or Roles.Subdireccion)
        {
            var carrerasActuales = await dbContext.UsuariosCarreras
                .Where(x => x.UsuarioId == usuario.Id)
                .ToListAsync(cancellationToken);
            dbContext.UsuariosCarreras.RemoveRange(carrerasActuales);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await userManager.UpdateSecurityStampAsync(usuario);
        await transaccion.CommitAsync(cancellationToken);

        return Ok(new ResultadoUsuarioDto(
            await MapearAsync(usuario, cancellationToken),
            null,
            "Usuario actualizado correctamente."));
    }

    [HttpPatch("{id}/estado")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ResultadoUsuarioDto>> CambiarEstado(
        string id,
        CambiarEstadoUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await ObtenerSinFiltroAsync(id, cancellationToken);
        if (usuario is null)
        {
            return NotFound();
        }

        if (!request.Activo && usuario.Id == userManager.GetUserId(User))
        {
            return BadRequest(new { mensaje = "No puedes desactivar tu propia cuenta." });
        }

        var roles = await userManager.GetRolesAsync(usuario);
        if (!request.Activo
            && roles.Contains(Roles.Administrador)
            && await EsUltimoAdministradorActivoAsync(usuario.Id, cancellationToken))
        {
            return BadRequest(new
            {
                mensaje = "No puedes desactivar al único administrador activo."
            });
        }

        usuario.Activo = request.Activo;
        var resultado = await userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            return BadRequest(new { mensaje = MensajeErrores(resultado) });
        }
        await userManager.UpdateSecurityStampAsync(usuario);

        return Ok(new ResultadoUsuarioDto(
            await MapearAsync(usuario, cancellationToken),
            null,
            request.Activo ? "Usuario activado correctamente." : "Usuario desactivado correctamente."));
    }

    [HttpPost("{id}/restablecer-contrasena")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ResultadoUsuarioDto>> RestablecerContrasena(
        string id,
        CancellationToken cancellationToken)
    {
        var usuario = await ObtenerSinFiltroAsync(id, cancellationToken);
        if (usuario is null)
        {
            return NotFound();
        }

        var contrasenaTemporal = GenerarContrasenaTemporal();
        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await userManager.ResetPasswordAsync(
            usuario,
            token,
            contrasenaTemporal);
        if (!resultado.Succeeded)
        {
            return BadRequest(new { mensaje = MensajeErrores(resultado) });
        }

        usuario.DebeCambiarContrasena = true;
        await userManager.UpdateAsync(usuario);

        return Ok(new ResultadoUsuarioDto(
            await MapearAsync(usuario, cancellationToken),
            contrasenaTemporal,
            "Contraseña restablecida correctamente."));
    }

    [HttpPatch("{id}/carreras")]
    public async Task<ActionResult<ResultadoUsuarioDto>> AsignarCarreras(
        string id,
        AsignarCarrerasUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await ObtenerSinFiltroAsync(id, cancellationToken);
        if (usuario is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(usuario);
        var rol = roles.FirstOrDefault();
        if (rol is Roles.Administrador or Roles.Subdireccion)
        {
            return BadRequest(new
            {
                mensaje = "Administrador y Subdirección tienen alcance institucional y no llevan carreras asignadas."
            });
        }

        var carrerasSolicitadas = request.Carreras.Distinct().ToArray();
        if (rol == Roles.Jefatura && carrerasSolicitadas.Length > 1)
        {
            return BadRequest(new
            {
                mensaje = "Jefatura sólo puede tener una carrera asignada."
            });
        }

        var carrerasExistentes = await dbContext.Carreras.CountAsync(
            x => carrerasSolicitadas.Contains(x.Id) && x.Activo,
            cancellationToken);
        if (carrerasExistentes != carrerasSolicitadas.Length)
        {
            return BadRequest(new
            {
                mensaje = "Una o más carreras seleccionadas no existen o están inactivas."
            });
        }

        var carrerasActuales = await dbContext.UsuariosCarreras
            .Where(x => x.UsuarioId == usuario.Id)
            .ToListAsync(cancellationToken);
        dbContext.UsuariosCarreras.RemoveRange(carrerasActuales);
        dbContext.UsuariosCarreras.AddRange(carrerasSolicitadas
            .Select(carreraId => new UsuarioCarrera
            {
                UsuarioId = usuario.Id,
                CarreraId = carreraId
            }));

        await dbContext.SaveChangesAsync(cancellationToken);
        await userManager.UpdateSecurityStampAsync(usuario);

        return Ok(new ResultadoUsuarioDto(
            await MapearAsync(usuario, cancellationToken),
            null,
            carrerasSolicitadas.Length == 0
                ? "Se retiraron las carreras asignadas."
                : "Carreras asignadas correctamente."));
    }

    private async Task<string?> ValidarRequestAsync(
        GuardarUsuarioRequest request,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        if (!Roles.Todos.Contains(request.Rol))
        {
            return "Selecciona un rol válido.";
        }

        var correoNormalizado = userManager.NormalizeEmail(request.Correo.Trim());
        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(
                x => x.Id != usuarioId && x.NormalizedEmail == correoNormalizado,
                cancellationToken))
        {
            return "Ya existe un usuario con ese correo.";
        }

        return null;
    }

    private async Task<UsuarioDto> MapearAsync(
        UsuarioAplicacion usuario,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(usuario);
        var carreras = await dbContext.UsuariosCarreras
            .AsNoTracking()
            .Where(x => x.UsuarioId == usuario.Id)
            .OrderBy(x => x.Carrera.Nombre)
            .Select(x => new { x.CarreraId, x.Carrera.Nombre })
            .ToListAsync(cancellationToken);

        return new UsuarioDto(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.Email ?? string.Empty,
            roles.FirstOrDefault() ?? string.Empty,
            carreras.Select(x => x.CarreraId).ToArray(),
            carreras.Select(x => x.Nombre).ToArray(),
            usuario.Activo,
            usuario.DebeCambiarContrasena,
            usuario.FechaAlta);
    }

    private Task<UsuarioAplicacion?> ObtenerSinFiltroAsync(
        string id,
        CancellationToken cancellationToken) =>
        dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    private async Task<bool> EsUltimoAdministradorActivoAsync(
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var administradoresActivos = await (
            from usuario in dbContext.Users.IgnoreQueryFilters()
            join usuarioRol in dbContext.UserRoles on usuario.Id equals usuarioRol.UserId
            join rol in dbContext.Roles on usuarioRol.RoleId equals rol.Id
            where usuario.Activo && rol.Name == Roles.Administrador
            select usuario.Id)
            .CountAsync(cancellationToken);

        var usuarioEsAdministrador = await userManager.IsInRoleAsync(
            await ObtenerSinFiltroAsync(usuarioId, cancellationToken)
                ?? throw new InvalidOperationException("Usuario no encontrado."),
            Roles.Administrador);

        return usuarioEsAdministrador && administradoresActivos <= 1;
    }

    private static string GenerarContrasenaTemporal()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string numeros = "23456789";
        const string simbolos = "!@$%*-_";
        const string todos = mayusculas + minusculas + numeros + simbolos;

        var caracteres = new List<char>
        {
            Aleatorio(mayusculas),
            Aleatorio(minusculas),
            Aleatorio(numeros),
            Aleatorio(simbolos)
        };
        while (caracteres.Count < 14)
        {
            caracteres.Add(Aleatorio(todos));
        }

        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string([.. caracteres]);
    }

    private static char Aleatorio(string caracteres) =>
        caracteres[RandomNumberGenerator.GetInt32(caracteres.Length)];

    private static string MensajeErrores(IdentityResult resultado) =>
        string.Join("; ", resultado.Errors.Select(x => x.Description));
}
