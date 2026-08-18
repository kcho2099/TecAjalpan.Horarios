using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Scheduling;

namespace TecAjalpan.Horarios.ArchitectureTests;

public sealed class GeneradorHorariosTests
{
    [Fact]
    public async Task ImpideCruceDeDocenteEntreCarreras()
    {
        var periodoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var fecha = new DateOnly(2026, 8, 29);
        var unidades = new[]
        {
            CrearUnidad(docenteId, Guid.NewGuid(), Guid.NewGuid(), fecha, Guid.NewGuid()),
            CrearUnidad(docenteId, Guid.NewGuid(), Guid.NewGuid(), fecha, Guid.NewGuid())
        };
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.Equal(2, resultado.HorasSolicitadas);
        Assert.Equal(1, resultado.HorasProgramadas);
        Assert.Single(resultado.Sesiones);
        Assert.Single(resultado.Pendientes);
    }

    [Fact]
    public async Task ImpideCruceDeAulaCompartida()
    {
        var periodoId = Guid.NewGuid();
        var espacioCompartido = Guid.NewGuid();
        var fecha = new DateOnly(2026, 8, 29);
        var unidades = new[]
        {
            CrearUnidad(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fecha, espacioCompartido),
            CrearUnidad(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fecha, espacioCompartido)
        };
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.Equal(1, resultado.HorasProgramadas);
        Assert.Single(resultado.Sesiones);
    }

    [Fact]
    public async Task PermiteMismoRecursoEnFechasDiferentes()
    {
        var periodoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var grupoId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var unidades = new[]
        {
            CrearUnidad(
                docenteId, grupoId, Guid.NewGuid(),
                new DateOnly(2026, 8, 29), espacioId),
            CrearUnidad(
                docenteId, grupoId, Guid.NewGuid(),
                new DateOnly(2026, 9, 5), espacioId)
        };
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.True(resultado.Completa);
        Assert.Equal(2, resultado.HorasProgramadas);
        Assert.Equal(2, resultado.Sesiones.Count);
    }

    private static UnidadGenerable CrearUnidad(
        Guid docenteId,
        Guid grupoId,
        Guid cargaId,
        DateOnly fecha,
        Guid espacioId) =>
        new(
            cargaId,
            docenteId,
            grupoId,
            1,
            [new OpcionGeneracion(espacioId, 6, 1, false, [fecha])]);

    private sealed class FuenteFalsa(DatosGeneracion datos) : IFuenteDatosGeneracion
    {
        public Task<DatosGeneracion> CargarAsync(
            SolicitudGeneracion solicitud,
            CancellationToken cancellationToken) => Task.FromResult(datos);
    }
}
