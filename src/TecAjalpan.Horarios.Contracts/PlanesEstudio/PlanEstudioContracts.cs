using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.PlanesEstudio;

public sealed record ModalidadPlanDto(Guid Id, string Clave, string Nombre);

public sealed record ReticulaDto(
    Guid Id,
    Guid CarreraId,
    string Clave,
    string Nombre,
    DateOnly InicioVigencia,
    DateOnly? FinVigencia,
    bool Activo,
    string RowVersion);

public sealed record MateriaDto(
    Guid Id,
    Guid ReticulaId,
    string Clave,
    string Nombre,
    byte Semestre,
    byte Creditos,
    byte HorasTeoricas,
    byte HorasPracticas,
    byte HorasSemanales,
    bool Activo,
    IReadOnlyList<ModalidadPlanDto> Modalidades,
    string RowVersion);

public sealed class GuardarReticulaRequest : IValidatableObject
{
    public Guid CarreraId { get; set; }

    [Required, StringLength(30, MinimumLength = 2)]
    public string Clave { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 3)]
    public string Nombre { get; set; } = string.Empty;

    public DateOnly InicioVigencia { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? FinVigencia { get; set; }
    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CarreraId == Guid.Empty)
            yield return new("Selecciona una carrera.", [nameof(CarreraId)]);
        if (FinVigencia.HasValue && FinVigencia < InicioVigencia)
            yield return new("La fecha final no puede ser anterior al inicio.", [nameof(FinVigencia)]);
    }
}

public sealed class GuardarMateriaRequest : IValidatableObject
{
    public Guid ReticulaId { get; set; }

    [Required, StringLength(30, MinimumLength = 2)]
    public string Clave { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 3)]
    public string Nombre { get; set; } = string.Empty;

    [Range(1, 8, ErrorMessage = "El semestre debe estar entre 1 y 8.")]
    public byte Semestre { get; set; } = 1;

    [Range(0, 30)]
    public byte Creditos { get; set; }

    [Range(0, 20)]
    public byte HorasTeoricas { get; set; }

    [Range(0, 20)]
    public byte HorasPracticas { get; set; }

    public List<Guid> ModalidadIds { get; set; } = [];
    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ReticulaId == Guid.Empty)
            yield return new("Selecciona una retícula.", [nameof(ReticulaId)]);
        if (HorasTeoricas + HorasPracticas == 0)
            yield return new("La materia debe tener al menos una hora semanal.");
        if (ModalidadIds.Count == 0)
            yield return new("Selecciona al menos una modalidad.", [nameof(ModalidadIds)]);
    }
}

public sealed class CambiarEstadoPlanRequest
{
    public bool Activo { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
