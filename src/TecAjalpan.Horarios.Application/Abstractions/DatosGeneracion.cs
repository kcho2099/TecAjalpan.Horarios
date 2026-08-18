namespace TecAjalpan.Horarios.Application.Abstractions;

public interface IFuenteDatosGeneracion
{
    Task<DatosGeneracion> CargarAsync(
        SolicitudGeneracion solicitud,
        CancellationToken cancellationToken);
}

public sealed record DatosGeneracion(
    Guid PeriodoId,
    IReadOnlyCollection<UnidadGenerable> Unidades,
    byte MaximoConsecutivasMateria = 2,
    IReadOnlyCollection<SesionFijaGeneracion>? SesionesFijas = null);

public sealed record SesionFijaGeneracion(
    SesionPropuesta Sesion,
    string Materia,
    string Docente,
    string Grupo,
    string Espacio);

public sealed class DatosGeneracionInvalidosException(string message)
    : InvalidOperationException(message);

public sealed record UnidadGenerable(
    Guid CargaAcademicaId,
    Guid DocenteId,
    Guid GrupoId,
    byte Numero,
    IReadOnlyCollection<OpcionGeneracion> Opciones,
    bool MantenerMismoEspacio = false,
    bool EsSabatina = false,
    byte Creditos = 0);

public sealed record OpcionGeneracion(
    Guid EspacioId,
    byte Dia,
    byte Bloque,
    bool Preferente,
    IReadOnlyCollection<DateOnly> Fechas);
