using System.ComponentModel.DataAnnotations;

namespace TecAjalpan.Horarios.Contracts.CargaAcademica;

public sealed record CargaAcademicaCatalogosDto(
    IReadOnlyList<CargaPeriodoDto> Periodos,
    IReadOnlyList<CargaCarreraDto> Carreras,
    IReadOnlyList<CargaModalidadDto> Modalidades,
    IReadOnlyList<CargaDocenteDto> Docentes);

public sealed record CargaPeriodoDto(Guid Id, string Nombre, byte Estado);
public sealed record CargaCarreraDto(Guid Id, string Clave, string Nombre);
public sealed record CargaModalidadDto(Guid Id, string Clave, string Nombre);
public sealed record CargaDocenteDto(
    Guid Id,
    string NumeroTrabajador,
    string NombreCompleto,
    byte CargaMaximaSemanal,
    IReadOnlyList<Guid> CarreraIds);

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
    int HorasAsignadas,
    byte CargaMaximaSemanal);

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
    IReadOnlyList<CargaAsignacionDto> Asignaciones,
    bool Completa,
    bool Autorizada);

public sealed record CargaAsignacionDto(
    Guid Id,
    Guid DocenteId,
    string DocenteNombre,
    byte Rol,
    string RolNombre,
    byte HorasAsignadas,
    byte Estado,
    string EstadoNombre,
    string? Observaciones,
    string RowVersion);

public sealed class GuardarCargaAcademicaRequest
{
    public Guid OfertaMateriaId { get; set; }
    public Guid DocenteTitularId { get; set; }

    [Range(1, 40, ErrorMessage = "Las horas del titular deben estar entre 1 y 40.")]
    public int HorasTitular { get; set; }

    public Guid? DocentePracticasId { get; set; }

    [Range(0, 40, ErrorMessage = "Las horas de prácticas deben estar entre 0 y 40.")]
    public int HorasPracticas { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    public List<CargaRowVersionDto> Versiones { get; set; } = [];
}

public sealed record CargaRowVersionDto(Guid Id, string RowVersion);

public sealed class AutorizarCargaAcademicaRequest
{
    [Required]
    public List<CargaRowVersionDto> Versiones { get; set; } = [];
}
