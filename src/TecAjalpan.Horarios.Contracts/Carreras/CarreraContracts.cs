using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Carreras;

public sealed record CarreraDto(
    Guid Id,
    string Clave,
    string Nombre,
    bool Activo,
    string RowVersion);

public sealed class GuardarCarreraRequest
{
    [Required(ErrorMessage = "La clave es obligatoria.")]
    [StringLength(30, MinimumLength = 2,
        ErrorMessage = "La clave debe tener entre 2 y 30 caracteres.")]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, MinimumLength = 3,
        ErrorMessage = "El nombre debe tener entre 3 y 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}

public sealed class CambiarEstadoCarreraRequest
{
    public bool Activo { get; set; }

    [Required(ErrorMessage = "La versión del registro es obligatoria.")]
    public string RowVersion { get; set; } = string.Empty;
}
