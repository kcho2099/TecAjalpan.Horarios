using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;

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
    public void ConfiguracionSabatinaDebeSumarDieciseisSemanas()
    {
        var configuracion = new ConfiguracionSabatina
        {
            Modulos =
            [
                CrearModulo(1, 5),
                CrearModulo(2, 5),
                CrearModulo(3, 5)
            ]
        };

        Assert.Throws<InvalidOperationException>(() => configuracion.Validar());
    }

    private static ModuloSabatino CrearModulo(byte orden, byte semanas) =>
        new()
        {
            Orden = orden,
            Semanas = semanas,
            Materias = [new ModuloMateria(), new ModuloMateria()]
        };
}
