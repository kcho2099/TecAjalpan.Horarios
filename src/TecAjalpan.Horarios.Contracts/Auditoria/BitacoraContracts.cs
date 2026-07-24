namespace TecAjalpan.Horarios.Contracts.Auditoria;

public sealed record BitacoraDto(
    long Id,
    string Entidad,
    string RegistroId,
    string Accion,
    string Usuario,
    DateTime Fecha,
    string? ValoresAnteriores,
    string? ValoresNuevos,
    string? CorrelationId);
