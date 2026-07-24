using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Infrastructure.Identity;

internal sealed class UsuarioClaimsPrincipalFactory(
    UserManager<UsuarioAplicacion> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options,
    ApplicationDbContext dbContext)
    : UserClaimsPrincipalFactory<UsuarioAplicacion, IdentityRole>(
        userManager,
        roleManager,
        options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UsuarioAplicacion user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var carreras = await dbContext.UsuariosCarreras
            .Where(x => x.UsuarioId == user.Id)
            .Select(x => x.CarreraId)
            .ToListAsync();

        identity.AddClaims(carreras.Select(x => new Claim("carrera", x.ToString())));
        identity.AddClaim(new Claim(
            "debe_cambiar_contrasena",
            user.DebeCambiarContrasena.ToString().ToLowerInvariant()));
        return identity;
    }
}
