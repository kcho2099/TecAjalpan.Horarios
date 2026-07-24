using Microsoft.AspNetCore.Identity;

namespace TecAjalpan.Horarios.Infrastructure.Identity;

public sealed class UsuarioAplicacion : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public bool DebeCambiarContrasena { get; set; } = true;
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
}
