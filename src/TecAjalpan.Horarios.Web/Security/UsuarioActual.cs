using System.Security.Claims;
using TecAjalpan.Horarios.Application.Abstractions;

namespace TecAjalpan.Horarios.Web.Security;

internal sealed class UsuarioActual(IHttpContextAccessor httpContextAccessor) : IUsuarioActual
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? UsuarioId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated == true;
    public bool TieneRol(string rol) => Principal?.IsInRole(rol) == true;

    public bool PuedeAccederCarrera(Guid carreraId) =>
        TieneRol(Application.Security.Roles.Administrador) ||
        TieneRol(Application.Security.Roles.Subdireccion) ||
        Principal?.FindAll("carrera")
            .Any(x => Guid.TryParse(x.Value, out var id) && id == carreraId) == true;
}
