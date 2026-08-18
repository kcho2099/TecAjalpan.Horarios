using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
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

    [Fact]
    public async Task ImpideMasDeDosHorasConsecutivasDeLaMismaMateriaEscolarizada()
    {
        var periodoId = Guid.NewGuid();
        var cargaId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var grupoId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var fecha = new DateOnly(2026, 8, 24);
        var opciones = Enumerable.Range(1, 4)
            .Select(x => new OpcionGeneracion(
                espacioId,
                1,
                checked((byte)x),
                false,
                [fecha]))
            .ToArray();
        var unidades = Enumerable.Range(1, 4)
            .Select(x => new UnidadGenerable(
                cargaId,
                docenteId,
                grupoId,
                checked((byte)x),
                opciones))
            .ToArray();
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades, 2)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.Equal(3, resultado.HorasProgramadas);
        var bloques = resultado.Sesiones.Select(x => (int)x.Bloque).OrderBy(x => x).ToArray();
        Assert.DoesNotContain(
            Enumerable.Range(1, 2),
            inicio => bloques.Contains(inicio)
                && bloques.Contains(inicio + 1)
                && bloques.Contains(inicio + 2));
    }

    [Fact]
    public async Task MateriaDeCincoCreditosRequiereAlMenosUnBloqueDoble()
    {
        var periodoId = Guid.NewGuid();
        var cargaId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var grupoId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var opciones = new[]
        {
            new OpcionGeneracion(espacioId, 1, 1, false, [new DateOnly(2026, 8, 24)]),
            new OpcionGeneracion(espacioId, 1, 2, false, [new DateOnly(2026, 8, 24)]),
            new OpcionGeneracion(espacioId, 2, 1, false, [new DateOnly(2026, 8, 25)]),
            new OpcionGeneracion(espacioId, 2, 3, false, [new DateOnly(2026, 8, 25)]),
            new OpcionGeneracion(espacioId, 3, 1, false, [new DateOnly(2026, 8, 26)])
        };
        var unidades = Enumerable.Range(1, 5)
            .Select(x => new UnidadGenerable(
                cargaId,
                docenteId,
                grupoId,
                checked((byte)x),
                opciones,
                Creditos: 5))
            .ToArray();
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades, 2)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.True(resultado.Completa);
        Assert.Equal(5, resultado.HorasProgramadas);
        Assert.Contains(
            resultado.Sesiones.GroupBy(x => x.Dia),
            dia => dia.Select(x => (int)x.Bloque).Distinct().Any(
                bloque => dia.Any(x => x.Bloque == bloque + 1)));
    }

    [Fact]
    public async Task MateriaDeCincoCreditosSinBloquesConsecutivosQuedaPendiente()
    {
        var periodoId = Guid.NewGuid();
        var cargaId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var opciones = new[]
        {
            new OpcionGeneracion(espacioId, 1, 1, false, [new DateOnly(2026, 8, 24)]),
            new OpcionGeneracion(espacioId, 1, 3, false, [new DateOnly(2026, 8, 24)]),
            new OpcionGeneracion(espacioId, 2, 1, false, [new DateOnly(2026, 8, 25)]),
            new OpcionGeneracion(espacioId, 2, 3, false, [new DateOnly(2026, 8, 25)])
        };
        var unidades = Enumerable.Range(1, 4)
            .Select(x => new UnidadGenerable(
                cargaId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                checked((byte)x),
                opciones,
                Creditos: 5))
            .ToArray();
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades, 2)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.False(resultado.Completa);
        Assert.Equal(0, resultado.HorasProgramadas);
        Assert.Single(resultado.Pendientes);
    }

    [Fact]
    public async Task MateriaDeCincoCreditosPuedeTenerDosBloquesDobles()
    {
        var periodoId = Guid.NewGuid();
        var cargaId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var grupoId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var opciones = new[]
        {
            new OpcionGeneracion(espacioId, 1, 1, false, [new DateOnly(2026, 8, 24)]),
            new OpcionGeneracion(espacioId, 1, 2, false, [new DateOnly(2026, 8, 24)]),
            new OpcionGeneracion(espacioId, 2, 1, false, [new DateOnly(2026, 8, 25)]),
            new OpcionGeneracion(espacioId, 2, 2, false, [new DateOnly(2026, 8, 25)])
        };
        var unidades = Enumerable.Range(1, 4)
            .Select(x => new UnidadGenerable(
                cargaId,
                docenteId,
                grupoId,
                checked((byte)x),
                opciones,
                Creditos: 5))
            .ToArray();
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades, 2)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.True(resultado.Completa);
        Assert.Equal(4, resultado.HorasProgramadas);
        Assert.Equal(2, resultado.Sesiones.GroupBy(x => x.Dia).Count());
        Assert.All(
            resultado.Sesiones.GroupBy(x => x.Dia),
            dia => Assert.Equal(2, dia.Count()));
    }

    [Fact]
    public async Task PrefiereHorarioCompactoSinHuecosParaGrupoYDocente()
    {
        var periodoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var grupoId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var fecha = new DateOnly(2026, 8, 24);
        var opciones = Enumerable.Range(1, 8)
            .Select(x => new OpcionGeneracion(
                espacioId,
                1,
                checked((byte)x),
                false,
                [fecha]))
            .ToArray();
        var unidades = Enumerable.Range(1, 3)
            .Select(x => new UnidadGenerable(
                Guid.NewGuid(),
                docenteId,
                grupoId,
                1,
                opciones))
            .ToArray();
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, unidades, 2)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.True(resultado.Completa);
        var bloques = resultado.Sesiones
            .Select(x => (int)x.Bloque)
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(3, bloques.Length);
        Assert.Equal(bloques[0] + 1, bloques[1]);
        Assert.Equal(bloques[1] + 1, bloques[2]);
    }

    [Fact]
    public async Task IntegraModuloSabatinoComoSesionesFijasSinOptimizarlo()
    {
        var periodoId = Guid.NewGuid();
        var cargaId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var grupoId = Guid.NewGuid();
        var espacioId = Guid.NewGuid();
        var fecha = new DateOnly(2026, 8, 29);
        var sesionesFijas = Enumerable.Range(1, 4)
            .Select(x => new SesionFijaGeneracion(
                new SesionPropuesta(
                    cargaId,
                    docenteId,
                    grupoId,
                    espacioId,
                    fecha,
                    6,
                    checked((byte)x),
                    true),
                "ACC-0001 · Materia sabatina",
                "Docente de prueba",
                "1SA",
                "A-1 · Aula 1"))
            .ToArray();
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, [], 2, sesionesFijas)));

        var resultado = await generador.GenerarAsync(
            new SolicitudGeneracion(periodoId, null, 10, false),
            CancellationToken.None);

        Assert.True(resultado.Completa);
        Assert.Equal(4, resultado.HorasProgramadas);
        int[] bloquesEsperados = [1, 2, 3, 4];
        Assert.Equal(
            bloquesEsperados,
            resultado.Sesiones.Select(x => (int)x.Bloque).OrderBy(x => x).ToArray());
        Assert.All(resultado.Sesiones, x => Assert.True(x.EsFija));
    }

    [Fact]
    public async Task RechazaCruceEntreSesionesSabatinasFijas()
    {
        var periodoId = Guid.NewGuid();
        var docenteId = Guid.NewGuid();
        var fecha = new DateOnly(2026, 8, 29);
        var sesiones = new[]
        {
            CrearSesionFija(Guid.NewGuid(), docenteId, Guid.NewGuid(), Guid.NewGuid(), fecha),
            CrearSesionFija(Guid.NewGuid(), docenteId, Guid.NewGuid(), Guid.NewGuid(), fecha)
        };
        var generador = new GeneradorHorariosCpSat(
            new FuenteFalsa(new DatosGeneracion(periodoId, [], 2, sesiones)));

        var excepcion = await Assert.ThrowsAsync<DatosGeneracionInvalidosException>(() =>
            generador.GenerarAsync(
                new SolicitudGeneracion(periodoId, null, 10, false),
                CancellationToken.None));

        Assert.Contains("Conflicto sabatino fijo", excepcion.Message);
        Assert.Contains("Docente de prueba", excepcion.Message);
    }

    [Fact]
    public void PermiteDescartarUnicamenteVersionEnBorrador()
    {
        var version = new HorarioVersion();

        version.Descartar();

        Assert.Equal(EstadoHorario.Descartado, version.Estado);
        Assert.Throws<InvalidOperationException>(() => version.Descartar());
    }

    private static SesionFijaGeneracion CrearSesionFija(
        Guid cargaId,
        Guid docenteId,
        Guid grupoId,
        Guid espacioId,
        DateOnly fecha) => new(
        new SesionPropuesta(
            cargaId,
            docenteId,
            grupoId,
            espacioId,
            fecha,
            6,
            1,
            true),
        "ACC-0001 · Materia sabatina",
        "Docente de prueba",
        "1SA",
        "A-1 · Aula 1");

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
