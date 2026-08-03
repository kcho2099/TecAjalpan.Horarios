using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.Oferta;

public sealed record OfertaCatalogosDto(
    IReadOnlyList<OfertaPeriodoDto> Periodos,
    IReadOnlyList<OfertaCarreraDto> Carreras,
    IReadOnlyList<OfertaModalidadDto> Modalidades,
    IReadOnlyList<OfertaEspacioDto> Espacios);

public sealed record OfertaPeriodoDto(
    Guid Id,
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    bool SemestresPares,
    bool PermitirExcepcionSemestre,
    byte Estado);

public sealed record OfertaCarreraDto(Guid Id, string Clave, string Nombre);

public sealed record OfertaModalidadDto(Guid Id, string Clave, string Nombre, byte Tipo);

public sealed record OfertaEspacioDto(
    Guid Id,
    Guid CarreraId,
    string Clave,
    string Nombre,
    string Tipo,
    short? Capacidad);

public sealed record PeriodoCarreraOfertaDto(
    Guid Id,
    Guid PeriodoId,
    Guid CarreraId,
    string CarreraClave,
    string CarreraNombre,
    Guid ModalidadId,
    string ModalidadNombre,
    IReadOnlyList<GrupoOfertaDto> Grupos,
    string RowVersion);

public sealed record GrupoOfertaDto(
    Guid Id,
    byte Semestre,
    string Clave,
    string Nombre,
    Guid? EspacioBaseId,
    string? EspacioBaseClave,
    string? EspacioBaseNombre,
    string? EspacioBaseTipo,
    IReadOnlyList<MateriaOfertaDto> Materias,
    ConfiguracionSabatinaOfertaDto? ConfiguracionSabatina,
    string RowVersion);

public sealed record MateriaOfertaDto(
    Guid Id,
    Guid MateriaId,
    string Clave,
    string Nombre,
    byte Creditos,
    byte HorasRequeridas,
    bool Activa,
    byte? Modulo,
    byte? Semanas,
    DateOnly? FechaInicioModulo,
    DateOnly? FechaFinModulo,
    byte? Turno,
    string? TurnoNombre);

public sealed record ConfiguracionSabatinaOfertaDto(
    Guid Id,
    DateOnly FechaInicio,
    bool Validada,
    IReadOnlyList<ModuloSabatinoOfertaDto> Modulos);

public sealed record ModuloSabatinoOfertaDto(
    Guid Id,
    byte Orden,
    byte Semanas,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    Guid MateriaMatutinaId,
    Guid MateriaVespertinaId);

public sealed record MateriaDisponibleOfertaDto(
    Guid Id,
    string Clave,
    string Nombre,
    byte Semestre,
    byte Creditos,
    byte HorasSemanales,
    string Reticula);

public sealed class CrearPeriodoCarreraRequest
{
    public Guid PeriodoId { get; set; }
    public Guid CarreraId { get; set; }
    public Guid ModalidadId { get; set; }
}

public sealed class GuardarGrupoOfertaRequest
{
    public Guid PeriodoCarreraId { get; set; }

    [Range(1, 9, ErrorMessage = "El semestre debe estar entre 1 y 9.")]
    public int Semestre { get; set; } = 1;

    [Required(ErrorMessage = "La clave del grupo es obligatoria.")]
    [StringLength(30, MinimumLength = 1)]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del grupo es obligatorio.")]
    [StringLength(120, MinimumLength = 2)]
    public string Nombre { get; set; } = string.Empty;

    public Guid EspacioBaseId { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class GuardarMateriasOfertaRequest
{
    public List<Guid> MateriaIds { get; set; } = [];

    [Required]
    public string RowVersionGrupo { get; set; } = string.Empty;
}

public sealed class GuardarConfiguracionSabatinaRequest
{
    public DateOnly FechaInicio { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(3)]
    public List<GuardarModuloSabatinoRequest> Modulos { get; set; } = [];

    [Required]
    public string RowVersionGrupo { get; set; } = string.Empty;
}

public sealed class GuardarModuloSabatinoRequest
{
    [Range(1, 3)]
    public byte Orden { get; set; }

    [Range(5, 6)]
    public byte Semanas { get; set; }

    public Guid MateriaMatutinaId { get; set; }
    public Guid MateriaVespertinaId { get; set; }
}

public sealed class EliminarOfertaRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
