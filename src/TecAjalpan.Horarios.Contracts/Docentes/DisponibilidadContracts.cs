using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Docentes;

public sealed record JornadaDocenteDto(
    byte Dia,
    TimeOnly HoraInicio,
    TimeOnly HoraFin);

public sealed record DisponibilidadBloqueDto(
    byte Dia,
    byte Bloque,
    bool Preferente);

public sealed record DisponibilidadDocenteDto(
    Guid? Id,
    Guid PeriodoId,
    Guid DocenteId,
    byte TipoDocente,
    bool Validada,
    DateTime? FechaValidacion,
    IReadOnlyList<JornadaDocenteDto> Jornadas,
    IReadOnlyList<DisponibilidadBloqueDto> Bloques,
    string? RowVersion);

public sealed class GuardarDisponibilidadDocenteRequest
{
    [Required]
    public Guid PeriodoId { get; set; }

    public List<JornadaDocenteDto> Jornadas { get; set; } = [];

    public List<DisponibilidadBloqueDto> Bloques { get; set; } = [];

    public string? RowVersion { get; set; }
}

public sealed record ValidarDisponibilidadRequest(string RowVersion);
