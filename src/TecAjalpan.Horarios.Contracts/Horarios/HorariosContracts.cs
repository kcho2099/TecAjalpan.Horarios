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
