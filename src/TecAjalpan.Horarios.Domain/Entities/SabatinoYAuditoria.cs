using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Domain.Entities;

public sealed class ConfiguracionSabatina : EntidadAuditable
{
    public Guid GrupoId { get; set; }
    public Grupo Grupo { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public bool Validada { get; set; }
    public ICollection<ModuloSabatino> Modulos { get; set; } = [];

    public void Validar()
    {
        var ordenados = Modulos.OrderBy(x => x.Orden).ToArray();
        if (ordenados.Length != 3 || ordenados.Sum(x => x.Semanas) != 16)
        {
            throw new InvalidOperationException("La configuración sabatina debe tener tres módulos que sumen 16 semanas.");
        }

        if (ordenados.Select(x => (int)x.Semanas).Order().SequenceEqual([5, 5, 6]) is false)
        {
            throw new InvalidOperationException("La distribución de módulos debe ser 5 + 5 + 6.");
        }

        if (ordenados.Any(x => x.Materias.Count != 2))
        {
            throw new InvalidOperationException("Cada módulo sabatino debe contener exactamente dos materias.");
        }

        Validada = true;
    }
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
