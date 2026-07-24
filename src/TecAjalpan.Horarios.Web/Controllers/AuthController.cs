using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Contracts.Auth;
using TecAjalpan.Horarios.Infrastructure.Identity;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("autenticacion")]
public sealed class AuthController(
    SignInManager<UsuarioAplicacion> signInManager,
    UserManager<UsuarioAplicacion> userManager,
    ApplicationDbContext dbContext) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<UsuarioSesionDto>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByEmailAsync(request.Correo);
        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new ResultadoOperacionDto(false, "Credenciales inválidas."));
        }

        var resultado = await signInManager.PasswordSignInAsync(
            usuario,
            request.Contrasena,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!resultado.Succeeded)
        {
            return Unauthorized(new ResultadoOperacionDto(
                false,
                resultado.IsLockedOut ? "Cuenta bloqueada temporalmente." : "Credenciales inválidas."));
        }

        return Ok(await CrearSesionAsync(usuario, cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioSesionDto>> Me(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (id is null)
        {
            return Unauthorized();
        }

        var usuario = await userManager.FindByIdAsync(id);

        return usuario is null
            ? Unauthorized()
            : Ok(await CrearSesionAsync(usuario, cancellationToken));
    }

    [Authorize]
    [HttpPost("cambiar-contrasena")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ResultadoOperacionDto>> CambiarContrasena(
        CambiarContrasenaRequest request)
    {
        var usuario = await userManager.GetUserAsync(User);
        if (usuario is null)
        {
            return Unauthorized();
        }

        var resultado = await userManager.ChangePasswordAsync(
            usuario,
            request.ContrasenaActual,
            request.ContrasenaNueva);

        if (!resultado.Succeeded)
        {
            return BadRequest(new ResultadoOperacionDto(
                false,
                string.Join("; ", resultado.Errors.Select(x => x.Description))));
        }

        usuario.DebeCambiarContrasena = false;
        await userManager.UpdateAsync(usuario);
        await signInManager.RefreshSignInAsync(usuario);
        return Ok(new ResultadoOperacionDto(true, "Contraseña actualizada."));
    }

    private async Task<UsuarioSesionDto> CrearSesionAsync(
        UsuarioAplicacion usuario,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(usuario);
        var carreras = await dbContext.UsuariosCarreras
            .Where(x => x.UsuarioId == usuario.Id)
            .Select(x => x.CarreraId)
            .ToListAsync(cancellationToken);

        return new UsuarioSesionDto(
            usuario.Id,
            usuario.Email ?? string.Empty,
            usuario.NombreCompleto,
            roles.ToArray(),
            carreras,
            usuario.DebeCambiarContrasena);
    }
}
