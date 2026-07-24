using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Domain.Entities;

public sealed class Carrera : CatalogoAuditable
{
    public ICollection<Reticula> Reticulas { get; set; } = [];
    public ICollection<Espacio> Espacios { get; set; } = [];
}

public sealed class Modalidad : CatalogoAuditable
{
    public TipoModalidad Tipo { get; set; }
}

public sealed class Reticula : CatalogoAuditable
{
    public Guid CarreraId { get; set; }
    public Carrera Carrera { get; set; } = null!;
    public DateOnly InicioVigencia { get; set; }
    public DateOnly? FinVigencia { get; set; }
    public ICollection<Materia> Materias { get; set; } = [];
}

public sealed class Materia : CatalogoAuditable
{
    public Guid ReticulaId { get; set; }
    public Reticula Reticula { get; set; } = null!;
    public byte Semestre { get; set; }
    public byte Creditos { get; set; }
    public byte HorasSemanales { get; set; }
}

public sealed class Periodo : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public byte Semanas { get; set; } = 16;
    public bool SemestresPares { get; set; }
    public bool PermitirExcepcionSemestre { get; set; }
    public EstadoPeriodo Estado { get; set; } = EstadoPeriodo.Configuracion;
    public ICollection<PeriodoCarrera> Carreras { get; set; } = [];
}

public sealed class PeriodoCarrera : EntidadAuditable
{
    public Guid PeriodoId { get; set; }
    public Periodo Periodo { get; set; } = null!;
    public Guid CarreraId { get; set; }
    public Carrera Carrera { get; set; } = null!;
    public Guid ModalidadId { get; set; }
    public Modalidad Modalidad { get; set; } = null!;
    public ICollection<Grupo> Grupos { get; set; } = [];
}

public sealed class Grupo : EntidadAuditable
{
    public Guid PeriodoCarreraId { get; set; }
    public PeriodoCarrera PeriodoCarrera { get; set; } = null!;
    public byte Semestre { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public ICollection<OfertaMateria> Oferta { get; set; } = [];
}

public sealed class OfertaMateria : EntidadAuditable
{
    public Guid GrupoId { get; set; }
    public Grupo Grupo { get; set; } = null!;
    public Guid MateriaId { get; set; }
    public Materia Materia { get; set; } = null!;
    public byte HorasRequeridas { get; set; }
    public bool Activa { get; set; } = true;
}
