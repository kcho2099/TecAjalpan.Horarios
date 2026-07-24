using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Usuarios;

public sealed record CarreraUsuarioDto(Guid Id, string Clave, string Nombre);

public sealed record UsuarioDto(
    string Id,
    string Nombre,
    string Correo,
    string Rol,
    IReadOnlyCollection<Guid> Carreras,
    IReadOnlyCollection<string> CarrerasNombres,
    bool Activo,
    bool DebeCambiarContrasena,
    DateTime FechaAlta);

public sealed record CatalogosUsuarioDto(
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<CarreraUsuarioDto> Carreras);

public sealed record ResultadoUsuarioDto(
    UsuarioDto Usuario,
    string? ContrasenaTemporal,
    string Mensaje);

public sealed class GuardarUsuarioRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, MinimumLength = 3,
        ErrorMessage = "El nombre debe tener entre 3 y 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(256, ErrorMessage = "El correo puede tener máximo 256 caracteres.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona un rol.")]
    public string Rol { get; set; } = string.Empty;
}

public sealed class CambiarEstadoUsuarioRequest
{
    public bool Activo { get; set; }
}

public sealed class AsignarCarrerasUsuarioRequest
{
    public List<Guid> Carreras { get; set; } = [];
}
