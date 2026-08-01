using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Domain.Entities;

public sealed class Docente : EntidadAuditable
{
    public string NumeroTrabajador { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public TipoDocente Tipo { get; set; }
    public byte HorasPermanenciaSemanal { get; set; } = 40;
    public byte CargaMaximaSemanal { get; set; } = 40;
    public bool Activo { get; set; } = true;
    public ICollection<DocenteCarrera> Carreras { get; set; } = [];
    public ICollection<DisponibilidadDocente> Disponibilidades { get; set; } = [];
}

public sealed class DocenteCarrera : EntidadAuditable
{
    public Guid DocenteId { get; set; }
    public Docente Docente { get; set; } = null!;
    public Guid CarreraId { get; set; }
    public Carrera Carrera { get; set; } = null!;
    public bool EsPrincipal { get; set; }
}

public sealed class DisponibilidadDocente : EntidadAuditable
{
    public Guid PeriodoId { get; set; }
    public Periodo Periodo { get; set; } = null!;
    public Guid DocenteId { get; set; }
    public Docente Docente { get; set; } = null!;
    public bool Validada { get; set; }
    public DateTime? FechaValidacion { get; set; }
    public string? UsuarioValida { get; set; }
    public ICollection<DisponibilidadBloque> Bloques { get; set; } = [];
    public ICollection<JornadaDocente> Jornadas { get; set; } = [];
}

public sealed class AutorizacionCargaDocente : EntidadAuditable
{
    public Guid PeriodoId { get; set; }
    public Periodo Periodo { get; set; } = null!;
    public Guid DocenteId { get; set; }
    public Docente Docente { get; set; } = null!;
    public byte HorasAutorizadas { get; set; }
}

public sealed class JornadaDocente : EntidadAuditable
{
    public Guid DisponibilidadDocenteId { get; set; }
    public DisponibilidadDocente DisponibilidadDocente { get; set; } = null!;
    public DiaAcademico Dia { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public bool EsSemanaSabatina { get; set; }
}

public sealed class DisponibilidadBloque : EntidadAuditable
{
    public Guid DisponibilidadDocenteId { get; set; }
    public DisponibilidadDocente DisponibilidadDocente { get; set; } = null!;
    public DiaAcademico Dia { get; set; }
    public byte Bloque { get; set; }
    public bool Disponible { get; set; } = true;
    public bool Preferente { get; set; }
}

public sealed class Espacio : CatalogoAuditable
{
    public Guid CarreraId { get; set; }
    public Carrera Carrera { get; set; } = null!;
    public string Tipo { get; set; } = "Aula";
    public short? Capacidad { get; set; }
    public string? Especialidad { get; set; }
}

public sealed class DisponibilidadEspacio : EntidadAuditable
{
    public Guid EspacioId { get; set; }
    public Espacio Espacio { get; set; } = null!;
    public Guid PeriodoId { get; set; }
    public Periodo Periodo { get; set; } = null!;
    public DiaAcademico Dia { get; set; }
    public byte Bloque { get; set; }
    public bool Disponible { get; set; } = true;
}

public sealed class CargaAcademica : EntidadAuditable
{
    public Guid OfertaMateriaId { get; set; }
    public OfertaMateria OfertaMateria { get; set; } = null!;
    public Guid DocenteId { get; set; }
    public Docente Docente { get; set; } = null!;
    public EstadoCarga Estado { get; set; } = EstadoCarga.Borrador;
    public string? Observaciones { get; set; }
    public DateTime? FechaAutorizacion { get; set; }
    public string? UsuarioAutoriza { get; set; }
}
