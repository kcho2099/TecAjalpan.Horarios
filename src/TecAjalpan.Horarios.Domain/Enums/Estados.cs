namespace TecAjalpan.Horarios.Domain.Enums;

public enum TipoModalidad : byte
{
    Escolarizada = 1,
    Sabatina = 2
}

public enum TipoDocente : byte
{
    TiempoCompleto = 1,
    Asignatura = 2
}

public enum DiaAcademico : byte
{
    Lunes = 1,
    Martes = 2,
    Miercoles = 3,
    Jueves = 4,
    Viernes = 5,
    Sabado = 6
}

public enum EstadoPeriodo : byte
{
    Configuracion = 1,
    Activo = 2,
    Cerrado = 3
}

public enum EstadoCarga : byte
{
    Borrador = 1,
    Autorizada = 2,
    Devuelta = 3
}

public enum RolCargaAcademica : byte
{
    Titular = 1,
    PracticasLaboratorio = 2
}

public enum EstadoHorario : byte
{
    Borrador = 1,
    EnRevision = 2,
    Aprobado = 3,
    Publicado = 4,
    Reemplazado = 5
}

public enum OrigenSesion : byte
{
    Automatica = 1,
    Manual = 2
}

public enum EstadoEjecucion : byte
{
    Pendiente = 1,
    Ejecutando = 2,
    Completada = 3,
    Fallida = 4,
    Cancelada = 5
}

public enum TurnoSabatino : byte
{
    Matutino = 1,
    Vespertino = 2
}
