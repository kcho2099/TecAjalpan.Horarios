namespace TecAjalpan.Horarios.Contracts.Horarios;

public sealed record PeriodoGeneracionDto(
    Guid Id,
    string Nombre,
    byte Estado,
    string EstadoTexto);

public sealed record GenerarHorarioRequest(
    Guid PeriodoId,
    int TiempoLimiteSegundos = 60);

public sealed record ResultadoGeneracionDto(
    Guid HorarioVersionId,
    int NumeroVersion,
    bool Completa,
    int HorasSolicitadas,
    int HorasProgramadas,
    int SesionesGeneradas,
    IReadOnlyCollection<PendienteGeneracionDto> Pendientes);

public sealed record PendienteGeneracionDto(
    Guid CargaAcademicaId,
    byte Horas,
    string Codigo,
    string Detalle);

public sealed record HorarioVersionResumenDto(
    Guid Id,
    int Numero,
    byte Estado,
    string EstadoTexto,
    string Origen,
    DateTime FechaCreacion,
    int HorasSolicitadas,
    int HorasProgramadas,
    int SesionesGeneradas,
    int NumeroPendientes,
    bool Completa);

public sealed record HorarioDetalleDto(
    Guid Id,
    Guid PeriodoId,
    string Periodo,
    int Numero,
    byte Estado,
    string EstadoTexto,
    string Origen,
    DateTime FechaCreacion,
    IReadOnlyCollection<HorarioSesionResumenDto> Sesiones,
    IReadOnlyCollection<PendienteGeneracionDto> Pendientes);

public sealed record HorarioSesionResumenDto(
    Guid CargaAcademicaId,
    Guid CarreraId,
    string Carrera,
    Guid ModalidadId,
    string Modalidad,
    Guid GrupoId,
    string Grupo,
    string MateriaClave,
    string Materia,
    Guid DocenteId,
    string Docente,
    string Espacio,
    byte Dia,
    string DiaTexto,
    byte Bloque,
    string HoraInicio,
    string HoraFin,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int NumeroSesiones);
