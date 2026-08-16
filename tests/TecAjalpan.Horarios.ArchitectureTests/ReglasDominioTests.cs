using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Domain.Rules;

namespace TecAjalpan.Horarios.ArchitectureTests;

public sealed class ReglasDominioTests
{
    [Fact]
    public void VersionPublicadaNoPuedeVolverAEditarSuEstado()
    {
        var version = new HorarioVersion();
        version.EnviarARevision();
        version.Aprobar();
        version.Publicar("subdireccion@ajalpan.tecnm.mx");

        Assert.Equal(EstadoHorario.Publicado, version.Estado);
        Assert.Throws<InvalidOperationException>(() => version.Aprobar());
    }

    [Fact]
    public void ConfiguracionSabatinaAceptaSieteMateriasEnDieciochoSemanas()
    {
        var inicio = new DateOnly(2026, 8, 15);
        var configuracion = new ConfiguracionSabatina
        {
            FechaInicio = inicio,
            Modulos =
            [
                CrearModulo(1, 5, TurnoSabatino.Matutino, inicio, 0),
                CrearModulo(2, 6, TurnoSabatino.Vespertino, inicio, 0),
                CrearModulo(3, 5, TurnoSabatino.Matutino, inicio, 5),
                CrearModulo(4, 6, TurnoSabatino.Vespertino, inicio, 6),
                CrearModulo(5, 4, TurnoSabatino.Matutino, inicio, 10),
                CrearModulo(6, 6, TurnoSabatino.Vespertino, inicio, 12),
                CrearModulo(7, 4, TurnoSabatino.Matutino, inicio, 14)
            ]
        };

        configuracion.Validar();

        Assert.True(configuracion.Validada);
    }

    [Fact]
    public void ConfiguracionSabatinaExigeDieciochoSemanasPorTurno()
    {
        var inicio = new DateOnly(2026, 8, 15);
        var configuracion = new ConfiguracionSabatina
        {
            FechaInicio = inicio,
            Modulos =
            [
                CrearModulo(1, 17, TurnoSabatino.Matutino, inicio, 0),
                CrearModulo(2, 18, TurnoSabatino.Vespertino, inicio, 0)
            ]
        };

        Assert.Throws<InvalidOperationException>(() => configuracion.Validar());
    }

    [Theory]
    [InlineData(DiaAcademico.Lunes, true)]
    [InlineData(DiaAcademico.Viernes, true)]
    [InlineData(DiaAcademico.Sabado, false)]
    public void EscolarizadaSoloPermiteLunesAViernes(
        DiaAcademico dia,
        bool permitido)
    {
        Assert.Equal(
            permitido,
            ReglasModalidad.PermiteProgramar(TipoModalidad.Escolarizada, dia));
    }

    [Theory]
    [InlineData(DiaAcademico.Lunes, false)]
    [InlineData(DiaAcademico.Sabado, true)]
    public void SabatinaSoloPermiteSabado(
        DiaAcademico dia,
        bool permitido)
    {
        Assert.Equal(
            permitido,
            ReglasModalidad.PermiteProgramar(TipoModalidad.Sabatina, dia));
    }

    private static ModuloSabatino CrearModulo(
        byte orden,
        byte semanas,
        TurnoSabatino turno,
        DateOnly inicio,
        int semanaInicial)
    {
        var fechaInicio = inicio.AddDays(semanaInicial * 7);
        var modulo = new ModuloSabatino
        {
            Orden = orden,
            Semanas = semanas,
            FechaInicio = fechaInicio,
            FechaFin = fechaInicio.AddDays((semanas - 1) * 7),
            Materias =
            [
                new ModuloMateria
                {
                    OfertaMateriaId = Guid.NewGuid(),
                    Turno = turno
                }
            ]
        };

        return modulo;
    }
}
