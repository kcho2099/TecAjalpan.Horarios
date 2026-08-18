using Google.OrTools.Sat;
using TecAjalpan.Horarios.Application.Abstractions;

namespace TecAjalpan.Horarios.Scheduling;

public sealed class GeneradorHorariosCpSat(
    IFuenteDatosGeneracion fuenteDatos) : IGeneradorHorarios
{
    public async Task<ResultadoGeneracion> GenerarAsync(
        SolicitudGeneracion solicitud,
        CancellationToken cancellationToken)
    {
        var datos = await fuenteDatos.CargarAsync(solicitud, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var sesionesFijas = datos.SesionesFijas ?? [];
        ValidarSesionesFijas(sesionesFijas);

        var modelo = new CpModel();
        var decisiones = new List<Decision>();

        foreach (var unidad in datos.Unidades)
        {
            var opciones = unidad.Opciones
                .Select((opcion, indice) => new Decision(
                    unidad,
                    opcion,
                    modelo.NewBoolVar(
                        $"c_{unidad.CargaAcademicaId:N}_{unidad.Numero}_{indice}")))
                .ToArray();

            if (opciones.Length > 0)
                modelo.Add(LinearExpr.Sum(opciones.Select(x => x.Variable)) <= 1);

            decisiones.AddRange(opciones);
        }

        AgregarRestriccionesDeCruce(modelo, decisiones);
        AgregarMaximoConsecutivasMateria(
            modelo,
            decisiones,
            datos.MaximoConsecutivasMateria);
        AgregarContinuidadDeEspacio(modelo, datos.Unidades, decisiones);

        if (decisiones.Count > 0)
        {
            var pesoProgramacion = datos.Unidades.Count + 1L;
            var objetivo = LinearExpr.NewBuilder();
            foreach (var decision in decisiones)
            {
                objetivo.AddTerm(
                    decision.Variable,
                    pesoProgramacion + (decision.Opcion.Preferente ? 1L : 0L));
            }

            modelo.Maximize(objetivo);
        }

        var solver = new CpSolver
        {
            StringParameters =
                $"max_time_in_seconds:{Math.Clamp(solicitud.TiempoLimiteSegundos, 1, 600)} "
                + "num_search_workers:8"
        };
        var estado = solver.Solve(modelo);
        var solucionDisponible = estado is CpSolverStatus.Feasible or CpSolverStatus.Optimal;

        var seleccionadas = solucionDisponible
            ? decisiones.Where(x => solver.BooleanValue(x.Variable)).ToArray()
            : [];
        var sesionesGeneradas = seleccionadas
            .SelectMany(x => x.Opcion.Fechas.Select(fecha => new SesionPropuesta(
                x.Unidad.CargaAcademicaId,
                x.Unidad.DocenteId,
                x.Unidad.GrupoId,
                x.Opcion.EspacioId,
                fecha,
                x.Opcion.Dia,
                x.Opcion.Bloque)))
            .ToArray();
        var sesiones = sesionesGeneradas
            .Concat(sesionesFijas.Select(x => x.Sesion))
            .OrderBy(x => x.Fecha)
            .ThenBy(x => x.Bloque)
            .ThenBy(x => x.GrupoId)
            .ToArray();

        var programadasPorCarga = seleccionadas
            .GroupBy(x => x.Unidad.CargaAcademicaId)
            .ToDictionary(x => x.Key, x => x.Count());
        var pendientes = datos.Unidades
            .GroupBy(x => x.CargaAcademicaId)
            .Select(x =>
            {
                var solicitadas = x.Count();
                var programadas = programadasPorCarga.GetValueOrDefault(x.Key);
                var faltantes = solicitadas - programadas;
                if (faltantes <= 0)
                    return null;

                var sinOpciones = x.Count(unidad => unidad.Opciones.Count == 0);
                var codigo = sinOpciones > 0
                    ? "SIN_CANDIDATOS"
                    : "SIN_CAPACIDAD";
                var detalle = sinOpciones > 0
                    ? $"{faltantes} h sin disponibilidad compatible de docente o espacio."
                    : $"{faltantes} h no pudieron acomodarse sin provocar cruces institucionales.";
                return new PendientePropuesto(
                    x.Key,
                    checked((byte)faltantes),
                    codigo,
                    detalle);
            })
            .Where(x => x is not null)
            .Cast<PendientePropuesto>()
            .ToArray();

        var horasFijas = sesionesFijas
            .Select(x => new
            {
                x.Sesion.CargaAcademicaId,
                x.Sesion.Dia,
                x.Sesion.Bloque
            })
            .Distinct()
            .Count();
        var horasSolicitadas = datos.Unidades.Count + horasFijas;
        var horasProgramadas = seleccionadas.Length + horasFijas;
        return new ResultadoGeneracion(
            solucionDisponible && horasProgramadas == horasSolicitadas,
            horasSolicitadas,
            horasProgramadas,
            sesiones,
            pendientes);
    }

    private static void ValidarSesionesFijas(
        IReadOnlyCollection<SesionFijaGeneracion> sesiones)
    {
        var cruceDocente = sesiones
            .GroupBy(x => new
            {
                x.Sesion.Fecha,
                x.Sesion.Bloque,
                x.Sesion.DocenteId
            })
            .FirstOrDefault(x => x
                .Select(y => y.Sesion.CargaAcademicaId)
                .Distinct()
                .Count() > 1);
        if (cruceDocente is not null)
        {
            var asignaciones = cruceDocente.Select(x => x.Materia).Distinct();
            throw new DatosGeneracionInvalidosException(
                $"Conflicto sabatino fijo: {cruceDocente.First().Docente} tiene "
                + $"{string.Join(" y ", asignaciones)} el {cruceDocente.Key.Fecha:dd/MM/yyyy} "
                + $"en el bloque {cruceDocente.Key.Bloque}.");
        }

        var cruceGrupo = sesiones
            .GroupBy(x => new
            {
                x.Sesion.Fecha,
                x.Sesion.Bloque,
                x.Sesion.GrupoId
            })
            .FirstOrDefault(x => x
                .Select(y => y.Sesion.CargaAcademicaId)
                .Distinct()
                .Count() > 1);
        if (cruceGrupo is not null)
        {
            throw new DatosGeneracionInvalidosException(
                $"Conflicto sabatino fijo: el grupo {cruceGrupo.First().Grupo} tiene más de una materia "
                + $"el {cruceGrupo.Key.Fecha:dd/MM/yyyy} en el bloque {cruceGrupo.Key.Bloque}.");
        }

        var cruceEspacio = sesiones
            .GroupBy(x => new
            {
                x.Sesion.Fecha,
                x.Sesion.Bloque,
                x.Sesion.EspacioId
            })
            .FirstOrDefault(x => x
                .Select(y => y.Sesion.CargaAcademicaId)
                .Distinct()
                .Count() > 1);
        if (cruceEspacio is not null)
        {
            var grupos = cruceEspacio.Select(x => x.Grupo).Distinct();
            throw new DatosGeneracionInvalidosException(
                $"Conflicto sabatino fijo: el aula {cruceEspacio.First().Espacio} está asignada a "
                + $"los grupos {string.Join(" y ", grupos)} el {cruceEspacio.Key.Fecha:dd/MM/yyyy} "
                + $"en el bloque {cruceEspacio.Key.Bloque}.");
        }
    }

    private static void AgregarRestriccionesDeCruce(
        CpModel modelo,
        IReadOnlyCollection<Decision> decisiones)
    {
        var recursos = new Dictionary<ClaveRecurso, List<BoolVar>>();

        foreach (var decision in decisiones)
        {
            foreach (var fecha in decision.Opcion.Fechas)
            {
                Agregar(recursos, new ClaveRecurso(
                    fecha, decision.Opcion.Bloque, 'D', decision.Unidad.DocenteId),
                    decision.Variable);
                Agregar(recursos, new ClaveRecurso(
                    fecha, decision.Opcion.Bloque, 'G', decision.Unidad.GrupoId),
                    decision.Variable);
                Agregar(recursos, new ClaveRecurso(
                    fecha, decision.Opcion.Bloque, 'E', decision.Opcion.EspacioId),
                    decision.Variable);
            }
        }

        foreach (var variables in recursos.Values.Where(x => x.Count > 1))
            modelo.Add(LinearExpr.Sum(variables) <= 1);
    }

    private static void AgregarContinuidadDeEspacio(
        CpModel modelo,
        IReadOnlyCollection<UnidadGenerable> unidades,
        IReadOnlyCollection<Decision> decisiones)
    {
        foreach (var carga in unidades
                     .Where(x => x.MantenerMismoEspacio)
                     .GroupBy(x => x.CargaAcademicaId))
        {
            var ordenadas = carga.OrderBy(x => x.Numero).ToArray();
            if (ordenadas.Length < 2)
                continue;

            var espacios = decisiones
                .Where(x => x.Unidad.CargaAcademicaId == carga.Key)
                .Select(x => x.Opcion.EspacioId)
                .Distinct();
            foreach (var espacioId in espacios)
            {
                var referencia = BuscarVariable(
                    decisiones, carga.Key, ordenadas[0].Numero, espacioId);
                foreach (var unidad in ordenadas.Skip(1))
                {
                    var actual = BuscarVariable(
                        decisiones, carga.Key, unidad.Numero, espacioId);
                    if (referencia is not null && actual is not null)
                        modelo.Add(referencia == actual);
                    else if (referencia is not null)
                        modelo.Add(referencia == 0);
                    else if (actual is not null)
                        modelo.Add(actual == 0);
                }
            }
        }
    }

    private static void AgregarMaximoConsecutivasMateria(
        CpModel modelo,
        IReadOnlyCollection<Decision> decisiones,
        byte maximoConfigurado)
    {
        var maximo = Math.Max(1, (int)maximoConfigurado);
        foreach (var carga in decisiones
                     .Where(x => !x.Unidad.EsSabatina)
                     .GroupBy(x => x.Unidad.CargaAcademicaId))
        {
            foreach (var dia in carga.Select(x => x.Opcion.Dia).Distinct())
            {
                var ultimoBloque = carga
                    .Where(x => x.Opcion.Dia == dia)
                    .Max(x => (int?)x.Opcion.Bloque) ?? 0;
                for (var inicio = 1; inicio + maximo <= ultimoBloque; inicio++)
                {
                    var fin = inicio + maximo;
                    var variables = carga
                        .Where(x => x.Opcion.Dia == dia
                            && x.Opcion.Bloque >= inicio
                            && x.Opcion.Bloque <= fin)
                        .Select(x => x.Variable)
                        .ToArray();
                    if (variables.Length > maximo)
                        modelo.Add(LinearExpr.Sum(variables) <= maximo);
                }
            }
        }
    }

    private static BoolVar? BuscarVariable(
        IEnumerable<Decision> decisiones,
        Guid cargaId,
        byte numeroUnidad,
        Guid espacioId) =>
        decisiones.FirstOrDefault(x =>
            x.Unidad.CargaAcademicaId == cargaId
            && x.Unidad.Numero == numeroUnidad
            && x.Opcion.EspacioId == espacioId)?.Variable;

    private static void Agregar(
        IDictionary<ClaveRecurso, List<BoolVar>> recursos,
        ClaveRecurso clave,
        BoolVar variable)
    {
        if (!recursos.TryGetValue(clave, out var variables))
        {
            variables = [];
            recursos.Add(clave, variables);
        }

        variables.Add(variable);
    }

    private sealed record Decision(
        UnidadGenerable Unidad,
        OpcionGeneracion Opcion,
        BoolVar Variable);

    private readonly record struct ClaveRecurso(
        DateOnly Fecha,
        byte Bloque,
        char Tipo,
        Guid RecursoId);
}
