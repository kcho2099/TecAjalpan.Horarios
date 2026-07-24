using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Docentes;

public sealed record DocenteCarreraDto(
    Guid Id,
    string Clave,
    string Nombre);

public sealed record DocenteDto(
    Guid Id,
    string NumeroTrabajador,
    string Nombres,
    string Apellidos,
    string Correo,
    byte Tipo,
    string TipoNombre,
    int HorasPermanenciaSemanal,
    int CargaMaximaSemanal,
    bool Activo,
    IReadOnlyList<DocenteCarreraDto> Carreras,
    string RowVersion);

public sealed class GuardarDocenteRequest
{
    [Required(ErrorMessage = "El número de trabajador es obligatorio.")]
    [StringLength(30, MinimumLength = 1,
        ErrorMessage = "El número de trabajador admite hasta 30 caracteres.")]
    public string NumeroTrabajador { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(120, MinimumLength = 2,
        ErrorMessage = "Los nombres deben tener entre 2 y 120 caracteres.")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(120, MinimumLength = 2,
        ErrorMessage = "Los apellidos deben tener entre 2 y 120 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(256, ErrorMessage = "El correo admite hasta 256 caracteres.")]
    public string Correo { get; set; } = string.Empty;

    [Range(1, 2, ErrorMessage = "Selecciona un tipo de contratación válido.")]
    public int Tipo { get; set; } = 1;

    [Range(1, 60, ErrorMessage = "Las horas de permanencia deben estar entre 1 y 60.")]
    public int HorasPermanenciaSemanal { get; set; } = 40;

    [Range(1, 60, ErrorMessage = "La carga máxima debe estar entre 1 y 60 horas.")]
    public int CargaMaximaSemanal { get; set; } = 40;

    [MinLength(1, ErrorMessage = "Asigna al menos una carrera.")]
    public List<Guid> CarreraIds { get; set; } = [];

    public string? RowVersion { get; set; }
}

public sealed class CambiarEstadoDocenteRequest
{
    public bool Activo { get; set; }

    [Required(ErrorMessage = "La versión del registro es obligatoria.")]
    public string RowVersion { get; set; } = string.Empty;
}
