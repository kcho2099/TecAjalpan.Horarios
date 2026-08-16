using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Domain.Entities;

public sealed class ConfiguracionSabatina : EntidadAuditable
{
    public const byte SemanasEfectivas = 18;

    public Guid GrupoId { get; set; }
    public Grupo Grupo { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public bool Validada { get; set; }
    public ICollection<ModuloSabatino> Modulos { get; set; } = [];

    public void Validar()
    {
        var ordenados = Modulos.OrderBy(x => x.Orden).ToArray();
        if (ordenados.Length is < 2 or > 36)
        {
            throw new InvalidOperationException(
                "La configuración sabatina debe contener entre 2 y 36 módulos.");
        }

        if (!ordenados.Select(x => (int)x.Orden)
                .SequenceEqual(Enumerable.Range(1, ordenados.Length)))
        {
            throw new InvalidOperationException(
                "Los módulos sabatinos deben tener un orden consecutivo.");
        }

        if (ordenados.Any(x => x.Semanas is < 1 or > SemanasEfectivas
            || x.Materias.Count != 1))
        {
            throw new InvalidOperationException(
                "Cada módulo sabatino debe contener una materia y durar entre 1 y 18 semanas.");
        }

        var materias = ordenados
            .Select(x => x.Materias.Single())
            .ToArray();
        if (materias.Select(x => x.OfertaMateriaId).Distinct().Count()
            != materias.Length)
        {
            throw new InvalidOperationException(
                "Cada materia debe aparecer una sola vez en la configuración sabatina.");
        }

        foreach (var turno in Enum.GetValues<TurnoSabatino>())
        {
            var modulosTurno = ordenados
                .Where(x => x.Materias.Single().Turno == turno)
                .ToArray();
            if (modulosTurno.Sum(x => x.Semanas) != SemanasEfectivas)
            {
                throw new InvalidOperationException(
                    $"Los módulos del turno {NombreTurno(turno)} deben sumar exactamente 18 semanas.");
            }

            var fechaEsperada = FechaInicio;
            foreach (var modulo in modulosTurno)
            {
                var fechaFinEsperada = fechaEsperada.AddDays((modulo.Semanas - 1) * 7);
                if (modulo.FechaInicio != fechaEsperada
                    || modulo.FechaFin != fechaFinEsperada)
                {
                    throw new InvalidOperationException(
                        $"Los módulos del turno {NombreTurno(turno)} deben ser consecutivos y comenzar en el primer sábado.");
                }

                fechaEsperada = fechaFinEsperada.AddDays(7);
            }
        }

        Validada = true;
    }

    private static string NombreTurno(TurnoSabatino turno) =>
        turno == TurnoSabatino.Matutino ? "matutino" : "vespertino";
}

public sealed class ModuloSabatino : EntidadAuditable
{
    public Guid ConfiguracionSabatinaId { get; set; }
    public ConfiguracionSabatina ConfiguracionSabatina { get; set; } = null!;
    public byte Orden { get; set; }
    public byte Semanas { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public ICollection<ModuloMateria> Materias { get; set; } = [];
}

public sealed class ModuloMateria : EntidadAuditable
{
    public Guid ModuloSabatinoId { get; set; }
    public ModuloSabatino ModuloSabatino { get; set; } = null!;
    public Guid OfertaMateriaId { get; set; }
    public OfertaMateria OfertaMateria { get; set; } = null!;
    public TurnoSabatino Turno { get; set; }
}

public sealed class UsuarioCarrera
{
    public string UsuarioId { get; set; } = string.Empty;
    public Guid CarreraId { get; set; }
    public Carrera Carrera { get; set; } = null!;
}

public sealed class Bitacora
{
    public long Id { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? ValoresAnteriores { get; set; }
    public string? ValoresNuevos { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class ConfiguracionSistema : EntidadAuditable
{
    public string NombreInstitucion { get; set; } = "Tecnológico de Ajalpan";
    public string ColorPrincipal { get; set; } = "#822427";
    public string ColorSecundario { get; set; } = "#FFFFFF";
    public string? RutaLogo { get; set; }
    public byte InicioEscolarizado { get; set; } = 8;
    public byte FinEscolarizado { get; set; } = 16;
    public byte DuracionBloqueMinutos { get; set; } = 60;
    public byte MaximoConsecutivasMateria { get; set; } = 2;
    public byte MaximoHorasDocenteDia { get; set; } = 8;
}
