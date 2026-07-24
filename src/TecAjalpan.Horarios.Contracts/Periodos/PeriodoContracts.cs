using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Periodos;

public sealed record PeriodoDto(
    Guid Id,
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    byte Semanas,
    bool SemestresPares,
    bool PermitirExcepcionSemestre,
    byte Estado,
    string EstadoTexto,
    string RowVersion);

public sealed class GuardarPeriodoRequest : IValidatableObject
{
    [Required(ErrorMessage = "El nombre del periodo es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre puede tener máximo 120 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public DateOnly FechaInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly FechaFin { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(5));

    [Range(1, 52, ErrorMessage = "Las semanas deben estar entre 1 y 52.")]
    public int Semanas { get; set; } = 16;

    public bool SemestresPares { get; set; }

    public bool PermitirExcepcionSemestre { get; set; }

    [Range(1, 3, ErrorMessage = "Selecciona un estado válido.")]
    public byte Estado { get; set; } = 1;

    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaFin <= FechaInicio)
        {
            yield return new ValidationResult(
                "La fecha de fin debe ser posterior a la fecha de inicio.",
                [nameof(FechaFin)]);
        }
    }
}

public sealed record ReabrirPeriodoRequest(string RowVersion);
