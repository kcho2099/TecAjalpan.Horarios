using TecAjalpan.Horarios.Domain.Entities;

namespace TecAjalpan.Horarios.Application.Abstractions;

public interface IUsuarioActual
{
    string? UsuarioId { get; }
    bool EstaAutenticado { get; }
    bool TieneRol(string rol);
    bool PuedeAccederCarrera(Guid carreraId);
}

public interface IFechaHora
{
    DateTime UtcNow { get; }
}

public interface IPeriodoRepository
{
    Task<Periodo?> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Periodo>> ListarAsync(CancellationToken cancellationToken);
    Task AgregarAsync(Periodo periodo, CancellationToken cancellationToken);
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}

public interface IHorarioRepository
{
    Task<HorarioVersion?> ObtenerVersionAsync(Guid id, CancellationToken cancellationToken);
    Task<int> SiguienteNumeroVersionAsync(Guid periodoId, Guid? periodoCarreraId, CancellationToken cancellationToken);
    Task AgregarVersionAsync(HorarioVersion version, CancellationToken cancellationToken);
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}

public interface IGeneradorHorarios
{
    Task<ResultadoGeneracion> GenerarAsync(SolicitudGeneracion solicitud, CancellationToken cancellationToken);
}

public sealed record SolicitudGeneracion(
    Guid PeriodoId,
    Guid? PeriodoCarreraId,
    int TiempoLimiteSegundos,
    bool ConservarAjustesManuales);

public sealed record ResultadoGeneracion(
    bool Completa,
    int HorasSolicitadas,
    int HorasProgramadas,
    IReadOnlyCollection<SesionPropuesta> Sesiones,
    IReadOnlyCollection<PendientePropuesto> Pendientes);

public sealed record SesionPropuesta(
    Guid CargaAcademicaId,
    Guid DocenteId,
    Guid GrupoId,
    Guid EspacioId,
    byte Dia,
    byte Bloque);

public sealed record PendientePropuesto(
    Guid CargaAcademicaId,
    byte Horas,
    string Codigo,
    string Detalle);
