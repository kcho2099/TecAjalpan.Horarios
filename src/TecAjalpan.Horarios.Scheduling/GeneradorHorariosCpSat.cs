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
        AgregarMaximoDiarioMateria(modelo, decisiones);
        AgregarMaximoDiarioDocenteGrupo(modelo, decisiones);
        AgregarBloqueDobleObligatorio(modelo, decisiones);
        AgregarContinuidadDeEspacio(modelo, datos.Unidades, decisiones);

        if (decisiones.Count > 0)
            modelo.Maximize(LinearExpr.Sum(decisiones.Select(x => x.Variable)));

        var tiempoLimite = Math.Clamp(solicitud.TiempoLimiteSegundos, 1, 600);
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var solverCobertura = CrearSolver(tiempoLimite);
        var estadoCobertura = solverCobertura.Solve(modelo);
        var solucionCoberturaDisponible =
            estadoCobertura is CpSolverStatus.Feasible or CpSolverStatus.Optimal;
        var programadasCobertura = solucionCoberturaDisponible
            ? decisiones.Count(x => solverCobertura.BooleanValue(x.Variable))
            : 0;
        var coberturaMaximaDemostrada = estadoCobertura == CpSolverStatus.Optimal
            || programadasCobertura == datos.Unidades.Count;

        var solverSeleccionado = solverCobertura;
        var estadoSeleccionado = estadoCobertura;
        var segundosRestantes = tiempoLimite
            - (int)Math.Ceiling(cronometro.Elapsed.TotalSeconds);
        if (decisiones.Count > 0
            && solucionCoberturaDisponible
            && coberturaMaximaDemostrada
            && segundosRestantes >= 1)
        {
            modelo.Add(
                LinearExpr.Sum(decisiones.Select(x => x.Variable))
                == programadasCobertura);
            AgregarObjetivoCalidad(modelo, decisiones);

            var solverCalidad = CrearSolver(segundosRestantes);
            var estadoCalidad = solverCalidad.Solve(modelo);
            if (estadoCalidad is CpSolverStatus.Feasible or CpSolverStatus.Optimal)
            {
                solverSeleccionado = solverCalidad;
                estadoSeleccionado = estadoCalidad;
            }
        }

        var solucionDisponible =
            estadoSeleccionado is CpSolverStatus.Feasible or CpSolverStatus.Optimal;
        var seleccionadas = solucionDisponible
            ? decisiones.Where(x => solverSeleccionado.BooleanValue(x.Variable)).ToArray()
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
                var busquedaConcluida = coberturaMaximaDemostrada;
                var resumenCandidatos = ResumirCandidatos(x);
                var codigo = sinOpciones > 0
                    ? "SIN_CANDIDATOS"
                    : busquedaConcluida
                        ? "SIN_CAPACIDAD"
                        : "LIMITE_DE_BUSQUEDA";
                var detalle = sinOpciones > 0
                    ? $"{faltantes} h sin disponibilidad validada de docente y aula compatible. "
                        + "Revisa la disponibilidad del docente, el aula base y las aulas compartidas de la carrera."
                    : busquedaConcluida
                        ? $"{faltantes} h no pudieron acomodarse respetando las reglas obligatorias. "
                            + resumenCandidatos
                        : $"{faltantes} h no quedaron programadas antes de alcanzar el límite de optimización; "
                            + "no se ha demostrado que falte capacidad. "
                            + resumenCandidatos;
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

    private static CpSolver CrearSolver(int tiempoLimiteSegundos) => new()
    {
        StringParameters =
            $"max_time_in_seconds:{Math.Max(1, tiempoLimiteSegundos)} "
            + "num_search_workers:8"
    };

    private static void AgregarObjetivoCalidad(
        CpModel modelo,
        List<Decision> decisiones)
    {
        var huecosGrupo = AgregarPenalizacionDeHuecos(
            modelo,
            decisiones,
            x => x.Unidad.GrupoId,
            "grupo");
        var huecosDocente = AgregarPenalizacionDeHuecos(
            modelo,
            decisiones,
            x => x.Unidad.DocenteId,
            "docente");
        var continuidadesDocenteGrupo =
            AgregarPenalizacionContinuidadDocenteGrupo(modelo, decisiones);

        const long pesoHuecoGrupo = 12L;
        const long pesoHuecoDocente = 10L;
        const long pesoContinuidadDocenteGrupo = 14L;
        const long pesoPreferencia = 1L;
        var objetivo = LinearExpr.NewBuilder();
        foreach (var decision in decisiones.Where(x => x.Opcion.Preferente))
            objetivo.AddTerm(decision.Variable, pesoPreferencia);
        foreach (var hueco in huecosGrupo)
            objetivo.AddTerm(hueco, -pesoHuecoGrupo);
        foreach (var hueco in huecosDocente)
            objetivo.AddTerm(hueco, -pesoHuecoDocente);
        foreach (var continuidad in continuidadesDocenteGrupo)
            objetivo.AddTerm(continuidad, -pesoContinuidadDocenteGrupo);

        modelo.Maximize(objetivo);
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

    private static void AgregarMaximoDiarioMateria(
        CpModel modelo,
        IReadOnlyCollection<Decision> decisiones)
    {
        const int maximo = 2;
        foreach (var carga in decisiones
                     .Where(x => !x.Unidad.EsSabatina)
                     .GroupBy(x => x.Unidad.CargaAcademicaId))
        {
            foreach (var dia in carga.Select(x => x.Opcion.Dia).Distinct())
            {
                var variables = carga
                    .Where(x => x.Opcion.Dia == dia)
                    .Select(x => x.Variable)
                    .Distinct()
                    .ToArray();
                if (variables.Length > maximo)
                    modelo.Add(LinearExpr.Sum(variables) <= maximo);
            }
        }
    }

    private static void AgregarMaximoDiarioDocenteGrupo(
        CpModel modelo,
        IReadOnlyCollection<Decision> decisiones)
    {
        const int maximoDiario = 4;
        foreach (var docenteGrupoDia in decisiones
                     .Where(x => !x.Unidad.EsSabatina)
                     .GroupBy(x => new
                     {
                         x.Unidad.DocenteId,
                         x.Unidad.GrupoId,
                         x.Opcion.Dia
                     }))
        {
            var variables = docenteGrupoDia
                .Select(x => x.Variable)
                .Distinct()
                .ToArray();
            if (variables.Length > maximoDiario)
                modelo.Add(LinearExpr.Sum(variables) <= maximoDiario);
        }
    }

    private static string ResumirCandidatos(
        IEnumerable<UnidadGenerable> unidades)
    {
        var carga = unidades.ToArray();
        var opciones = carga
            .SelectMany(x => x.Opciones)
            .GroupBy(x => new { x.Dia, x.Bloque, x.EspacioId })
            .Select(x => x.Key)
            .ToArray();
        if (opciones.Length == 0)
            return string.Empty;

        var dias = opciones
            .Select(x => x.Dia)
            .Distinct()
            .OrderBy(x => x)
            .Select(TextoDia)
            .ToArray();
        var aulas = opciones.Select(x => x.EspacioId).Distinct().Count();
        var requiereDoble = carga.First().Creditos >= 5
            ? " La materia requiere al menos un bloque doble por tener 5 o más créditos."
            : string.Empty;
        return $"Antes de considerar cruces globales tenía {opciones.Length} combinación(es) "
            + $"en {string.Join(", ", dias)} y {aulas} aula(s). "
            + "Se aplican máximo 2 h de la materia al día y máximo 4 h del docente con el grupo al día."
            + requiereDoble;
    }

    private static string TextoDia(byte dia) => dia switch
    {
        1 => "lunes",
        2 => "martes",
        3 => "miércoles",
        4 => "jueves",
        5 => "viernes",
        6 => "sábado",
        _ => $"día {dia}"
    };

    private static void AgregarBloqueDobleObligatorio(
        CpModel modelo,
        IReadOnlyCollection<Decision> decisiones)
    {
        foreach (var carga in decisiones
                     .Where(x => !x.Unidad.EsSabatina && x.Unidad.Creditos >= 5)
                     .GroupBy(x => x.Unidad.CargaAcademicaId))
        {
            var ocupaciones = new Dictionary<(byte Dia, byte Bloque), BoolVar>();
            foreach (var bloque in carga.GroupBy(x => new
                     {
                         x.Opcion.Dia,
                         x.Opcion.Bloque
                     }))
            {
                var ocupada = modelo.NewBoolVar(
                    $"materia_{carga.Key:N}_{bloque.Key.Dia}_{bloque.Key.Bloque}");
                var variables = bloque.Select(x => x.Variable).Distinct().ToArray();
                foreach (var variable in variables)
                    modelo.Add(ocupada >= variable);
                modelo.Add(ocupada <= LinearExpr.Sum(variables));
                ocupaciones.Add((bloque.Key.Dia, bloque.Key.Bloque), ocupada);
            }

            var algunaHora = CrearDisyuncion(
                modelo,
                ocupaciones.Values,
                $"materia_programada_{carga.Key:N}");
            var parejas = new List<BoolVar>();
            foreach (var dia in ocupaciones.Keys.Select(x => x.Dia).Distinct())
            {
                var bloques = ocupaciones.Keys
                    .Where(x => x.Dia == dia)
                    .Select(x => x.Bloque)
                    .OrderBy(x => x)
                    .ToArray();
                foreach (var bloque in bloques.Where(x => x < byte.MaxValue))
                {
                    if (!ocupaciones.TryGetValue(
                            (dia, checked((byte)(bloque + 1))),
                            out var siguiente))
                    {
                        continue;
                    }

                    var actual = ocupaciones[(dia, bloque)];
                    var pareja = modelo.NewBoolVar(
                        $"doble_{carga.Key:N}_{dia}_{bloque}");
                    modelo.Add(pareja <= actual);
                    modelo.Add(pareja <= siguiente);
                    modelo.Add(pareja >= actual + siguiente - 1);
                    parejas.Add(pareja);
                }
            }

            if (parejas.Count == 0)
                modelo.Add(algunaHora == 0);
            else
                modelo.Add(LinearExpr.Sum(parejas) >= algunaHora);
        }
    }

    private static List<BoolVar> AgregarPenalizacionDeHuecos(
        CpModel modelo,
        List<Decision> decisiones,
        Func<Decision, Guid> seleccionarRecurso,
        string nombreRecurso)
    {
        const int primerBloque = 1;
        const int ultimoBloque = 8;
        var huecos = new List<BoolVar>();
        var decisionesEscolarizadas = decisiones
            .Where(x => !x.Unidad.EsSabatina)
            .ToArray();

        foreach (var recursoDia in decisionesEscolarizadas.GroupBy(x => new
                 {
                     RecursoId = seleccionarRecurso(x),
                     x.Opcion.Dia
                 }))
        {
            var ocupacion = new Dictionary<int, BoolVar>();
            for (var bloque = primerBloque; bloque <= ultimoBloque; bloque++)
            {
                var variables = recursoDia
                    .Where(x => x.Opcion.Bloque == bloque)
                    .Select(x => x.Variable)
                    .Distinct()
                    .ToArray();
                var ocupado = modelo.NewBoolVar(
                    $"ocupado_{nombreRecurso}_{recursoDia.Key.RecursoId:N}_{recursoDia.Key.Dia}_{bloque}");
                ocupacion.Add(bloque, ocupado);

                if (variables.Length == 0)
                {
                    modelo.Add(ocupado == 0);
                    continue;
                }

                foreach (var variable in variables)
                    modelo.Add(ocupado >= variable);
                modelo.Add(ocupado <= LinearExpr.Sum(variables));
            }

            for (var bloque = primerBloque + 1; bloque < ultimoBloque; bloque++)
            {
                var hayClaseAntes = CrearDisyuncion(
                    modelo,
                    ocupacion.Where(x => x.Key < bloque).Select(x => x.Value),
                    $"antes_{nombreRecurso}_{recursoDia.Key.RecursoId:N}_{recursoDia.Key.Dia}_{bloque}");
                var hayClaseDespues = CrearDisyuncion(
                    modelo,
                    ocupacion.Where(x => x.Key > bloque).Select(x => x.Value),
                    $"despues_{nombreRecurso}_{recursoDia.Key.RecursoId:N}_{recursoDia.Key.Dia}_{bloque}");
                var hueco = modelo.NewBoolVar(
                    $"hueco_{nombreRecurso}_{recursoDia.Key.RecursoId:N}_{recursoDia.Key.Dia}_{bloque}");

                modelo.Add(hueco <= hayClaseAntes);
                modelo.Add(hueco <= hayClaseDespues);
                modelo.Add(hueco + ocupacion[bloque] <= 1);
                modelo.Add(hueco >= hayClaseAntes + hayClaseDespues - ocupacion[bloque] - 1);
                huecos.Add(hueco);
            }
        }

        return huecos;
    }

    private static List<BoolVar> AgregarPenalizacionContinuidadDocenteGrupo(
        CpModel modelo,
        IReadOnlyCollection<Decision> decisiones)
    {
        var continuidades = new List<BoolVar>();
        foreach (var docenteGrupoDia in decisiones
                     .Where(x => !x.Unidad.EsSabatina)
                     .GroupBy(x => new
                     {
                         x.Unidad.DocenteId,
                         x.Unidad.GrupoId,
                         x.Opcion.Dia
                     }))
        {
            var ocupaciones = new Dictionary<(Guid CargaId, int Bloque), BoolVar>();
            foreach (var cargaBloque in docenteGrupoDia.GroupBy(x => new
                     {
                         x.Unidad.CargaAcademicaId,
                         x.Opcion.Bloque
                     }))
            {
                ocupaciones.Add(
                    (cargaBloque.Key.CargaAcademicaId, cargaBloque.Key.Bloque),
                    CrearDisyuncion(
                        modelo,
                        cargaBloque.Select(x => x.Variable),
                        $"ocupacion_docente_grupo_materia_"
                        + $"{docenteGrupoDia.Key.DocenteId:N}_"
                        + $"{docenteGrupoDia.Key.GrupoId:N}_"
                        + $"{docenteGrupoDia.Key.Dia}_"
                        + $"{cargaBloque.Key.CargaAcademicaId:N}_"
                        + $"{cargaBloque.Key.Bloque}"));
            }

            var bloques = ocupaciones.Keys
                .Select(x => x.Bloque)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
            foreach (var bloque in bloques)
            {
                var actuales = ocupaciones
                    .Where(x => x.Key.Bloque == bloque)
                    .ToArray();
                var siguientes = ocupaciones
                    .Where(x => x.Key.Bloque == bloque + 1)
                    .ToArray();

                foreach (var actual in actuales)
                {
                    foreach (var siguiente in siguientes.Where(x =>
                                 x.Key.CargaId != actual.Key.CargaId))
                    {
                        var continuidad = modelo.NewBoolVar(
                            $"continuidad_docente_grupo_"
                            + $"{docenteGrupoDia.Key.DocenteId:N}_"
                            + $"{docenteGrupoDia.Key.GrupoId:N}_"
                            + $"{docenteGrupoDia.Key.Dia}_{bloque}_"
                            + $"{actual.Key.CargaId:N}_"
                            + $"{siguiente.Key.CargaId:N}");
                        modelo.Add(continuidad <= actual.Value);
                        modelo.Add(continuidad <= siguiente.Value);
                        modelo.Add(continuidad >=
                            actual.Value + siguiente.Value - 1);
                        continuidades.Add(continuidad);
                    }
                }
            }
        }

        return continuidades;
    }

    private static BoolVar CrearDisyuncion(
        CpModel modelo,
        IEnumerable<BoolVar> variables,
        string nombre)
    {
        var elementos = variables.Distinct().ToArray();
        var resultado = modelo.NewBoolVar(nombre);
        if (elementos.Length == 0)
        {
            modelo.Add(resultado == 0);
            return resultado;
        }

        foreach (var elemento in elementos)
            modelo.Add(resultado >= elemento);
        modelo.Add(resultado <= LinearExpr.Sum(elementos));
        return resultado;
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
