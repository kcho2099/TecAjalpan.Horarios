using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Domain.Entities;

public sealed class HorarioVersion : EntidadAuditable
{
    public Guid PeriodoId { get; set; }
    public Periodo Periodo { get; set; } = null!;
    public Guid? PeriodoCarreraId { get; set; }
    public PeriodoCarrera? PeriodoCarrera { get; set; }
    public int Numero { get; set; }
    public EstadoHorario Estado { get; private set; } = EstadoHorario.Borrador;
    public string Origen { get; set; } = "Generación";
    public DateTime? FechaPublicacion { get; private set; }
    public string? UsuarioPublica { get; private set; }
    public ICollection<SesionHorario> Sesiones { get; set; } = [];
    public ICollection<PendienteGeneracion> Pendientes { get; set; } = [];

    public void EnviarARevision()
    {
        ExigirEstado(EstadoHorario.Borrador);
        Estado = EstadoHorario.EnRevision;
    }

    public void Aprobar()
    {
        ExigirEstado(EstadoHorario.EnRevision);
        Estado = EstadoHorario.Aprobado;
    }

    public void Rechazar()
    {
        ExigirEstado(EstadoHorario.EnRevision);
        Estado = EstadoHorario.Borrador;
    }

    public void Publicar(string usuario)
    {
        ExigirEstado(EstadoHorario.Aprobado);
        Estado = EstadoHorario.Publicado;
        UsuarioPublica = usuario;
        FechaPublicacion = DateTime.UtcNow;
    }

    public void Reemplazar()
    {
        ExigirEstado(EstadoHorario.Publicado);
        Estado = EstadoHorario.Reemplazado;
    }

    private void ExigirEstado(EstadoHorario esperado)
    {
        if (Estado != esperado)
        {
            throw new InvalidOperationException($"La versión debe estar en estado {esperado}.");
        }
    }
}

public sealed class SesionHorario : EntidadAuditable
{
    public Guid HorarioVersionId { get; set; }
    public HorarioVersion HorarioVersion { get; set; } = null!;
    public Guid CargaAcademicaId { get; set; }
    public CargaAcademica CargaAcademica { get; set; } = null!;
    public Guid DocenteId { get; set; }
    public Guid GrupoId { get; set; }
    public Guid EspacioId { get; set; }
    public Espacio Espacio { get; set; } = null!;
    public DiaAcademico Dia { get; set; }
    public byte Bloque { get; set; }
    public byte DuracionBloques { get; set; } = 1;
    public OrigenSesion Origen { get; set; } = OrigenSesion.Automatica;
    public bool FijadaParaRegeneracion { get; set; }
}

public sealed class PendienteGeneracion : EntidadAuditable
{
    public Guid HorarioVersionId { get; set; }
    public HorarioVersion HorarioVersion { get; set; } = null!;
    public Guid CargaAcademicaId { get; set; }
    public CargaAcademica CargaAcademica { get; set; } = null!;
    public byte HorasPendientes { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}

public sealed class EjecucionGenerador : EntidadAuditable
{
    public Guid PeriodoId { get; set; }
    public Guid? PeriodoCarreraId { get; set; }
    public EstadoEjecucion Estado { get; set; } = EstadoEjecucion.Pendiente;
    public DateTime? Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public int TiempoLimiteSegundos { get; set; } = 60;
    public int HorasSolicitadas { get; set; }
    public int HorasProgramadas { get; set; }
    public string? Mensaje { get; set; }
}

public sealed class RevisionHorario : EntidadAuditable
{
    public Guid HorarioVersionId { get; set; }
    public HorarioVersion HorarioVersion { get; set; } = null!;
    public string UsuarioId { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

public sealed class AjusteManual : EntidadAuditable
{
    public Guid SesionHorarioId { get; set; }
    public SesionHorario SesionHorario { get; set; } = null!;
    public string ValoresAnteriores { get; set; } = "{}";
    public string ValoresNuevos { get; set; } = "{}";
}
