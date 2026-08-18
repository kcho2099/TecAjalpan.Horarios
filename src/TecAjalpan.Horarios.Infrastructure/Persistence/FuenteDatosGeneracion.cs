using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Infrastructure.Persistence;

internal sealed class FuenteDatosGeneracion(
    ApplicationDbContext dbContext) : IFuenteDatosGeneracion
{
    public async Task<DatosGeneracion> CargarAsync(
        SolicitudGeneracion solicitud,
        CancellationToken cancellationToken)
    {
        var periodo = await dbContext.Periodos.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == solicitud.PeriodoId, cancellationToken)
            ?? throw new InvalidOperationException("No se encontró el periodo solicitado.");

        var cargas = await dbContext.CargasAcademicas.AsNoTracking()
            .Where(x => !x.Eliminado
                && x.Estado == EstadoCarga.Autorizada
                && !x.OfertaMateria.Eliminado
                && x.OfertaMateria.Activa
                && !x.OfertaMateria.Grupo.Eliminado
                && !x.OfertaMateria.Grupo.PeriodoCarrera.Eliminado
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == solicitud.PeriodoId)
            .Include(x => x.OfertaMateria)
                .ThenInclude(x => x.Materia)
            .Include(x => x.OfertaMateria)
                .ThenInclude(x => x.Grupo)
                    .ThenInclude(x => x.PeriodoCarrera)
                        .ThenInclude(x => x.Modalidad)
            .Include(x => x.Docente)
            .ToArrayAsync(cancellationToken);

        var docentesIds = cargas.Select(x => x.DocenteId).Distinct().ToArray();
        var disponibilidades = await dbContext.DisponibilidadesDocentes.AsNoTracking()
            .Where(x => !x.Eliminado
                && x.PeriodoId == solicitud.PeriodoId
                && x.Validada
                && docentesIds.Contains(x.DocenteId))
            .Include(x => x.Docente)
            .Include(x => x.Bloques)
            .Include(x => x.Jornadas)
            .ToDictionaryAsync(x => x.DocenteId, cancellationToken);

        var espacios = await dbContext.Espacios.AsNoTracking()
            .Where(x => !x.Eliminado && x.Activo)
            .Include(x => x.CarrerasCompartidas)
            .ToArrayAsync(cancellationToken);
        var disponibilidadesEspacios = await dbContext.DisponibilidadesEspacios
            .AsNoTracking()
            .Where(x => !x.Eliminado && x.PeriodoId == solicitud.PeriodoId)
            .ToArrayAsync(cancellationToken);
        var modulos = await dbContext.ModulosMaterias.AsNoTracking()
            .Where(x => !x.Eliminado
                && !x.ModuloSabatino.Eliminado
                && !x.ModuloSabatino.ConfiguracionSabatina.Eliminado
                && x.ModuloSabatino.ConfiguracionSabatina.Validada
                && !x.OfertaMateria.Eliminado
                && x.OfertaMateria.Grupo.PeriodoCarrera.PeriodoId == solicitud.PeriodoId)
            .Include(x => x.ModuloSabatino)
            .ToDictionaryAsync(x => x.OfertaMateriaId, cancellationToken);
        var configuracion = await dbContext.ConfiguracionSistema.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken) ?? new ConfiguracionSistema();

        var unidades = new List<UnidadGenerable>();
        var sesionesFijas = new List<SesionFijaGeneracion>();
        foreach (var carga in cargas)
        {
            disponibilidades.TryGetValue(carga.DocenteId, out var disponibilidad);
            var grupo = carga.OfertaMateria.Grupo;
            var carreraId = grupo.PeriodoCarrera.CarreraId;
            var espaciosCarga = EspaciosPermitidos(grupo, carreraId, espacios);

            if (grupo.PeriodoCarrera.Modalidad.Tipo == TipoModalidad.Sabatina)
            {
                AgregarSabatinoFijo(
                    sesionesFijas, carga, disponibilidad, espaciosCarga,
                    disponibilidadesEspacios, modulos.GetValueOrDefault(carga.OfertaMateriaId));
            }
            else
            {
                AgregarEscolarizado(
                    unidades, carga, disponibilidad, espaciosCarga,
                    disponibilidadesEspacios, periodo, configuracion);
            }
        }

        return new DatosGeneracion(
            periodo.Id,
            unidades,
            configuracion.MaximoConsecutivasMateria,
            sesionesFijas);
    }

    private static Espacio[] EspaciosPermitidos(
        Grupo grupo,
        Guid carreraId,
        IReadOnlyCollection<Espacio> espacios)
    {
        var permitidos = espacios.Where(x =>
                x.CarreraId == carreraId
                || x.CarrerasCompartidas.Any(c => !c.Eliminado && c.CarreraId == carreraId))
            .ToArray();

        return grupo.EspacioBaseId.HasValue
            ? permitidos.Where(x => x.Id == grupo.EspacioBaseId.Value).ToArray()
            : permitidos;
    }

    private static void AgregarEscolarizado(
        List<UnidadGenerable> unidades,
        CargaAcademica carga,
        DisponibilidadDocente? disponibilidad,
        IReadOnlyCollection<Espacio> espacios,
        IReadOnlyCollection<DisponibilidadEspacio> disponibilidadesEspacios,
        Periodo periodo,
        ConfiguracionSistema configuracion)
    {
        var opciones = new List<OpcionGeneracion>();
        var duracionMinutos = Math.Max(1, (int)configuracion.DuracionBloqueMinutos);
        var totalBloques = Math.Max(0,
            (configuracion.FinEscolarizado - configuracion.InicioEscolarizado) * 60
            / duracionMinutos);

        foreach (var dia in Enum.GetValues<DiaAcademico>().Where(x => x != DiaAcademico.Sabado))
        {
            var fechas = FechasDelDia(periodo.FechaInicio, periodo.FechaFin, dia);
            for (byte bloque = 1; bloque <= totalBloques; bloque++)
            {
                if (!DocenteDisponible(disponibilidad, dia, bloque, configuracion))
                    continue;

                foreach (var espacio in espacios.Where(x =>
                             EspacioDisponible(disponibilidadesEspacios, x.Id, dia, bloque)))
                {
                    opciones.Add(new OpcionGeneracion(
                        espacio.Id,
                        (byte)dia,
                        bloque,
                        EsPreferente(disponibilidad, dia, bloque),
                        fechas));
                }
            }
        }

        for (byte numero = 1; numero <= carga.OfertaMateria.HorasRequeridas; numero++)
        {
            unidades.Add(new UnidadGenerable(
                carga.Id,
                carga.DocenteId,
                carga.OfertaMateria.GrupoId,
                numero,
                opciones,
                Creditos: carga.OfertaMateria.Materia.Creditos));
        }
    }

    private static void AgregarSabatinoFijo(
        List<SesionFijaGeneracion> sesiones,
        CargaAcademica carga,
        DisponibilidadDocente? disponibilidad,
        IReadOnlyCollection<Espacio> espacios,
        IReadOnlyCollection<DisponibilidadEspacio> disponibilidadesEspacios,
        ModuloMateria? moduloMateria)
    {
        var grupo = carga.OfertaMateria.Grupo;
        var materia = carga.OfertaMateria.Materia;
        var docente = carga.Docente;
        if (moduloMateria is null)
        {
            throw new DatosGeneracionInvalidosException(
                $"La materia sabatina {materia.Clave} · {materia.Nombre} del grupo "
                + $"{grupo.Clave} no tiene un módulo configurado.");
        }
        if (!grupo.EspacioBaseId.HasValue)
        {
            throw new DatosGeneracionInvalidosException(
                $"El grupo sabatino {grupo.Clave} no tiene un aula base asignada.");
        }

        var espacio = espacios.SingleOrDefault(x => x.Id == grupo.EspacioBaseId.Value)
            ?? throw new DatosGeneracionInvalidosException(
                $"El aula base del grupo sabatino {grupo.Clave} no está activa o no está permitida para su carrera.");
        var turno = moduloMateria.Turno;
        var inicioBloque = turno == TurnoSabatino.Matutino ? 1 : 5;
        var fechas = FechasSabatinas(
            moduloMateria.ModuloSabatino.FechaInicio,
            moduloMateria.ModuloSabatino.FechaFin);
        if (fechas.Count == 0)
        {
            throw new DatosGeneracionInvalidosException(
                $"El módulo sabatino de {materia.Clave} · {materia.Nombre} no contiene sábados efectivos.");
        }

        for (byte posicion = 0; posicion < 4; posicion++)
        {
            var bloque = checked((byte)(inicioBloque + posicion));
            if (!DocenteDisponibleSabatino(disponibilidad, bloque))
            {
                throw new DatosGeneracionInvalidosException(
                    $"El docente {docente.Apellidos}, {docente.Nombres} no tiene disponibilidad validada "
                    + $"para {materia.Clave} el sábado en el bloque {bloque}.");
            }
            if (!EspacioDisponible(
                    disponibilidadesEspacios,
                    espacio.Id,
                    DiaAcademico.Sabado,
                    bloque))
            {
                throw new DatosGeneracionInvalidosException(
                    $"El aula {espacio.Clave} · {espacio.Nombre} no está disponible el sábado "
                    + $"en el bloque {bloque} para el grupo {grupo.Clave}.");
            }

            foreach (var fecha in fechas)
            {
                sesiones.Add(new SesionFijaGeneracion(
                    new SesionPropuesta(
                        carga.Id,
                        carga.DocenteId,
                        carga.OfertaMateria.GrupoId,
                        espacio.Id,
                        fecha,
                        (byte)DiaAcademico.Sabado,
                        bloque,
                        true),
                    $"{materia.Clave} · {materia.Nombre}",
                    $"{docente.Apellidos}, {docente.Nombres}",
                    grupo.Clave,
                    $"{espacio.Clave} · {espacio.Nombre}"));
            }
        }
    }

    private static bool DocenteDisponible(
        DisponibilidadDocente? disponibilidad,
        DiaAcademico dia,
        byte bloque,
        ConfiguracionSistema configuracion)
    {
        if (disponibilidad is null)
            return false;
        if (disponibilidad.Docente.Tipo == TipoDocente.Asignatura)
        {
            return disponibilidad.Bloques.Any(x =>
                !x.Eliminado && x.Dia == dia && x.Bloque == bloque && x.Disponible);
        }

        var inicio = new TimeOnly(configuracion.InicioEscolarizado, 0)
            .AddMinutes((bloque - 1) * configuracion.DuracionBloqueMinutos);
        var fin = inicio.AddMinutes(configuracion.DuracionBloqueMinutos);
        return disponibilidad.Jornadas.Any(x =>
            !x.Eliminado && x.Dia == dia && x.HoraInicio <= inicio && x.HoraFin >= fin);
    }

    private static bool DocenteDisponibleSabatino(
        DisponibilidadDocente? disponibilidad,
        byte bloque)
    {
        if (disponibilidad is null)
            return false;
        if (disponibilidad.Docente.Tipo == TipoDocente.Asignatura)
        {
            return disponibilidad.Bloques.Any(x =>
                !x.Eliminado
                && x.Dia == DiaAcademico.Sabado
                && x.Bloque == bloque
                && x.Disponible);
        }

        var inicio = new TimeOnly(8 + bloque - 1, 0);
        var fin = inicio.AddHours(1);
        return disponibilidad.Jornadas.Any(x =>
            !x.Eliminado
            && x.Dia == DiaAcademico.Sabado
            && x.HoraInicio <= inicio
            && x.HoraFin >= fin);
    }

    private static bool EsPreferente(
        DisponibilidadDocente? disponibilidad,
        DiaAcademico dia,
        byte bloque) =>
        disponibilidad?.Bloques.Any(x =>
            !x.Eliminado
            && x.Dia == dia
            && x.Bloque == bloque
            && x.Disponible
            && x.Preferente) == true;

    private static bool EspacioDisponible(
        IReadOnlyCollection<DisponibilidadEspacio> disponibilidades,
        Guid espacioId,
        DiaAcademico dia,
        byte bloque)
    {
        var configurada = disponibilidades.FirstOrDefault(x =>
            x.EspacioId == espacioId && x.Dia == dia && x.Bloque == bloque);
        return configurada?.Disponible ?? true;
    }

    private static List<DateOnly> FechasDelDia(
        DateOnly inicio,
        DateOnly fin,
        DiaAcademico dia)
    {
        var resultado = new List<DateOnly>();
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
        {
            if (ConvertirDia(fecha.DayOfWeek) == dia)
                resultado.Add(fecha);
        }

        return resultado;
    }

    private static List<DateOnly> FechasSabatinas(
        DateOnly inicio,
        DateOnly fin)
    {
        var resultado = new List<DateOnly>();
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(7))
            resultado.Add(fecha);
        return resultado;
    }

    private static DiaAcademico? ConvertirDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => DiaAcademico.Lunes,
        DayOfWeek.Tuesday => DiaAcademico.Martes,
        DayOfWeek.Wednesday => DiaAcademico.Miercoles,
        DayOfWeek.Thursday => DiaAcademico.Jueves,
        DayOfWeek.Friday => DiaAcademico.Viernes,
        DayOfWeek.Saturday => DiaAcademico.Sabado,
        _ => null
    };
}
