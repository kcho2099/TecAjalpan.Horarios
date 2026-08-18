namespace TecAjalpan.Horarios.Application.Abstractions;

public interface IFuenteDatosGeneracion
{
    Task<DatosGeneracion> CargarAsync(
        SolicitudGeneracion solicitud,
        CancellationToken cancellationToken);
}

public sealed record DatosGeneracion(
    Guid PeriodoId,
    IReadOnlyCollection<UnidadGenerable> Unidades);

public sealed record UnidadGenerable(
    Guid CargaAcademicaId,
    Guid DocenteId,
    Guid GrupoId,
    byte Numero,
    IReadOnlyCollection<OpcionGeneracion> Opciones,
    bool MantenerMismoEspacio = false);

public sealed record OpcionGeneracion(
    Guid EspacioId,
    byte Dia,
    byte Bloque,
    bool Preferente,
    IReadOnlyCollection<DateOnly> Fechas);
