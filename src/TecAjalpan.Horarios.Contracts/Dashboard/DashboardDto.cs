namespace TecAjalpan.Horarios.Contracts.Dashboard;

public sealed record DashboardDto(
    int PeriodosEnConfiguracion,
    int VersionesEnRevision,
    int CargasPendientes,
    int PendientesGeneracion);
