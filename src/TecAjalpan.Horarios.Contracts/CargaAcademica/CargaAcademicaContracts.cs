using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.CargaAcademica;

public sealed record CargaAcademicaCatalogosDto(
    IReadOnlyList<CargaPeriodoDto> Periodos,
    IReadOnlyList<CargaCarreraDto> Carreras,
    IReadOnlyList<CargaModalidadDto> Modalidades);

public sealed record CargaPeriodoDto(Guid Id, string Nombre, byte Estado);
public sealed record CargaCarreraDto(Guid Id, string Clave, string Nombre);
public sealed record CargaModalidadDto(Guid Id, string Clave, string Nombre);
public sealed record CargaConfiguracionDto(
    Guid PeriodoCarreraId,
    Guid PeriodoId,
    Guid CarreraId,
    Guid ModalidadId,
    string CarreraNombre,
    string ModalidadNombre,
    IReadOnlyList<CargaGrupoDto> Grupos,
    IReadOnlyList<CargaDocenteResumenDto> ResumenDocentes);

public sealed record CargaDocenteResumenDto(
    Guid DocenteId,
    string DocenteNombre,
    byte TipoDocente,
    int HorasAsignadas,
    byte CargaMaximaSemanal,
    int? HorasDisponibles,
    int HorasAsignadasModalidad,
    int HorasDisponiblesModalidad);

public sealed record CargaGrupoDto(
    Guid Id,
    byte Semestre,
    string Clave,
    string Nombre,
    string? EspacioBase,
    IReadOnlyList<CargaMateriaDto> Materias);

public sealed record CargaMateriaDto(
    Guid OfertaMateriaId,
    Guid MateriaId,
    string Clave,
    string Nombre,
    byte HorasRequeridas,
    byte HorasTeoricas,
    byte HorasPracticas,
    CargaTitularDto? Titular,
    bool Autorizada);

public sealed record CargaTitularDto(
    Guid Id,
    Guid DocenteId,
    string DocenteNombre,
    byte Estado,
    string EstadoNombre,
    string? Observaciones,
    string RowVersion);

public sealed class GuardarCargaAcademicaRequest
{
    public Guid OfertaMateriaId { get; set; }
    public Guid DocenteId { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class AutorizarCargaAcademicaRequest
{
    public Guid CargaAcademicaId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class QuitarTitularCargaAcademicaRequest
{
    public Guid CargaAcademicaId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
