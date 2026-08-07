using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Espacios;

public sealed record EspaciosCatalogoDto(
    IReadOnlyList<EspacioCarreraDto> Carreras,
    IReadOnlyList<EspacioDto> Espacios);

public sealed record EspacioCarreraDto(
    Guid Id,
    string Clave,
    string Nombre,
    bool Activo,
    bool PuedeAdministrar);

public sealed record EspacioDto(
    Guid Id,
    Guid CarreraId,
    string CarreraClave,
    string CarreraNombre,
    string Clave,
    string Nombre,
    string Tipo,
    short? Capacidad,
    string? Especialidad,
    IReadOnlyList<Guid> CarreraIdsCompartidas,
    bool Activo,
    string RowVersion);

public sealed class GuardarEspacioRequest : IValidatableObject
{
    public Guid CarreraId { get; set; }

    [Required(ErrorMessage = "La clave es obligatoria.")]
    [StringLength(30, MinimumLength = 1,
        ErrorMessage = "La clave debe tener entre 1 y 30 caracteres.")]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, MinimumLength = 2,
        ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo de espacio.")]
    public string Tipo { get; set; } = "Aula";

    [Range(1, short.MaxValue,
        ErrorMessage = "La capacidad debe ser mayor que cero.")]
    public short? Capacidad { get; set; }

    [StringLength(120,
        ErrorMessage = "La especialidad no puede exceder 120 caracteres.")]
    public string? Especialidad { get; set; }

    public List<Guid> CarreraIdsCompartidas { get; set; } = [];

    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CarreraId == Guid.Empty)
        {
            yield return new(
                "Selecciona la carrera a la que pertenece el espacio.",
                [nameof(CarreraId)]);
        }

        if (Tipo is not ("Aula" or "Laboratorio"))
        {
            yield return new(
                "El tipo debe ser Aula o Laboratorio.",
                [nameof(Tipo)]);
        }

        if (CarreraIdsCompartidas.Contains(Guid.Empty))
        {
            yield return new(
                "Selecciona carreras válidas para compartir el espacio.",
                [nameof(CarreraIdsCompartidas)]);
        }

        if (CarreraIdsCompartidas.Contains(CarreraId))
        {
            yield return new(
                "La carrera responsable ya tiene acceso al espacio y no debe agregarse como compartida.",
                [nameof(CarreraIdsCompartidas)]);
        }
    }
}

public sealed class CambiarEstadoEspacioRequest
{
    public bool Activo { get; set; }

    [Required(ErrorMessage = "La versión del registro es obligatoria.")]
    public string RowVersion { get; set; } = string.Empty;
}
