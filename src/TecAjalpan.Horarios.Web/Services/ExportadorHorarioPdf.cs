using System.Globalization;
using System.Text;
using TecAjalpan.Horarios.Contracts.Horarios;

namespace TecAjalpan.Horarios.Web.Services;

internal static class ExportadorHorarioPdf
{
    private const double AnchoPagina = 595;
    private const double AltoPagina = 842;
    private const double Margen = 24;
    private const double AltoEncabezadoDias = 26;
    private const double AltoFila = 75;
    private const double AnchoHora = 43;
    private const double TopeCuadricula = 735;
    private static readonly string[] Dias =
        ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO"];
    private static readonly ColorPdf[] Colores =
    [
        new(0.96, 0.90, 0.90, 0.55, 0.12, 0.14),
        new(0.93, 0.90, 0.97, 0.39, 0.23, 0.58),
        new(0.96, 0.93, 0.84, 0.48, 0.31, 0.08),
        new(0.88, 0.95, 0.92, 0.08, 0.40, 0.25),
        new(0.88, 0.93, 0.97, 0.10, 0.33, 0.53)
    ];

    public static byte[] Crear(
        string periodo,
        int numeroVersion,
        string estado,
        int pendientes,
        HorarioSesionResumenDto[] sesiones,
        Guid? grupoId,
        Guid? docenteId)
    {
        var paginas = ConstruirPaginas(sesiones, grupoId, docenteId);
        var contenidos = paginas
            .Select((pagina, indice) => DibujarPagina(
                pagina,
                periodo,
                numeroVersion,
                estado,
                pendientes,
                indice + 1,
                paginas.Length))
            .ToArray();
        return CrearDocumento(contenidos);
    }

    private static PaginaPdf[] ConstruirPaginas(
        HorarioSesionResumenDto[] sesiones,
        Guid? grupoId,
        Guid? docenteId)
    {
        if (sesiones.Length == 0)
            return [new PaginaPdf("Sin sesiones", "No hay clases con los filtros seleccionados.", false, [])];

        var paginas = new List<PaginaPdf>();
        if (grupoId.HasValue)
        {
            foreach (var grupo in sesiones
                         .GroupBy(x => new { x.GrupoId, x.Grupo, x.Carrera, x.Modalidad })
                         .OrderBy(x => x.Key.Carrera)
                         .ThenBy(x => x.Key.Grupo))
            {
                AgregarPaginasPorVigencia(
                    paginas,
                    $"Grupo {grupo.Key.Grupo}",
                    $"{grupo.Key.Carrera} - {grupo.Key.Modalidad}",
                    false,
                    grupo.ToArray());
            }
            return paginas.ToArray();
        }

        if (docenteId.HasValue)
        {
            foreach (var docente in sesiones
                         .GroupBy(x => new { x.DocenteId, x.Docente })
                         .OrderBy(x => x.Key.Docente))
            {
                AgregarPaginasPorVigencia(
                    paginas,
                    docente.Key.Docente,
                    "Horario del docente - incluye todas las carreras y grupos filtrados",
                    true,
                    docente.ToArray());
            }
            return paginas.ToArray();
        }

        foreach (var grupo in sesiones
                     .GroupBy(x => new { x.GrupoId, x.Grupo, x.Carrera, x.Modalidad })
                     .OrderBy(x => x.Key.Carrera)
                     .ThenBy(x => x.Key.Grupo))
        {
            AgregarPaginasPorVigencia(
                paginas,
                $"Grupo {grupo.Key.Grupo}",
                $"{grupo.Key.Carrera} - {grupo.Key.Modalidad}",
                false,
                grupo.ToArray());
        }
        return paginas.ToArray();
    }

    private static void AgregarPaginasPorVigencia(
        List<PaginaPdf> paginas,
        string titulo,
        string subtitulo,
        bool mostrarGrupo,
        HorarioSesionResumenDto[] sesiones)
    {
        var escolarizadas = sesiones.Where(x => x.Dia != 6).ToArray();
        if (escolarizadas.Length > 0)
        {
            paginas.Add(new PaginaPdf(
                titulo,
                $"{subtitulo} - Escolarizado",
                mostrarGrupo,
                escolarizadas));
        }

        foreach (var vigencia in sesiones
                     .Where(x => x.Dia == 6)
                     .GroupBy(x => new { x.FechaInicio, x.FechaFin })
                     .OrderBy(x => x.Key.FechaInicio))
        {
            var distribuciones = SepararSuperposiciones(vigencia.ToArray());
            for (var indice = 0; indice < distribuciones.Length; indice++)
            {
                var numeroHoja = distribuciones.Length > 1
                    ? $" - hoja {indice + 1} de {distribuciones.Length}"
                    : string.Empty;
                var descripcionModulo =
                    $"Módulo sabatino {vigencia.Key.FechaInicio:dd/MM/yyyy}-{vigencia.Key.FechaFin:dd/MM/yyyy}{numeroHoja}";
                paginas.Add(new PaginaPdf(
                    titulo,
                    mostrarGrupo ? descripcionModulo : $"{subtitulo} - {descripcionModulo}",
                    mostrarGrupo,
                    distribuciones[indice]));
            }
        }
    }

    private static HorarioSesionResumenDto[][] SepararSuperposiciones(
        HorarioSesionResumenDto[] sesiones)
    {
        var paginas = new List<List<HorarioSesionResumenDto>>();
        foreach (var carga in sesiones
                     .GroupBy(x => x.CargaAcademicaId)
                     .OrderBy(x => x.Min(y => y.Bloque)))
        {
            var posiciones = carga.Select(x => (x.Dia, x.Bloque)).ToHashSet();
            var pagina = paginas.FirstOrDefault(x =>
                !x.Any(y => posiciones.Contains((y.Dia, y.Bloque))));
            if (pagina is null)
            {
                pagina = [];
                paginas.Add(pagina);
            }
            pagina.AddRange(carga);
        }

        return paginas.Select(x => x.ToArray()).ToArray();
    }

    private static byte[] DibujarPagina(
        PaginaPdf pagina,
        string periodo,
        int numeroVersion,
        string estado,
        int pendientes,
        int numeroPagina,
        int totalPaginas)
    {
        var lienzo = new LienzoPdf();
        lienzo.Rectangulo(0, 0, AnchoPagina, AltoPagina, new ColorPdf(0.98, 0.98, 0.97, 0, 0, 0));
        lienzo.Texto(Margen, 809, "PLANEACIÓN INSTITUCIONAL", 7, true, new ColorPdf(0, 0, 0, 0.51, 0.14, 0.15));
        lienzo.Texto(Margen, 789, "Horario semanal", 17, true, ColorPdf.Texto);
        lienzo.Texto(Margen, 771, pagina.Titulo, 11, true, ColorPdf.Texto);
        lienzo.Texto(Margen, 757, pagina.Subtitulo, 7, false, ColorPdf.Secundario);

        const double anchoEtiqueta = 78;
        lienzo.Rectangulo(
            AnchoPagina - Margen - anchoEtiqueta,
            786,
            anchoEtiqueta,
            20,
            new ColorPdf(0.96, 0.90, 0.90, 0, 0, 0));
        lienzo.TextoCentrado(
            AnchoPagina - Margen - anchoEtiqueta,
            793,
            anchoEtiqueta,
            $"{estado.ToUpperInvariant()} · V{numeroVersion}",
            7,
            true,
            new ColorPdf(0, 0, 0, 0.51, 0.14, 0.15));
        lienzo.TextoDerecha(
            AnchoPagina - Margen,
            765,
            periodo,
            7,
            false,
            ColorPdf.Secundario);
        lienzo.TextoDerecha(
            AnchoPagina - Margen,
            752,
            pendientes == 0 ? "Sin pendientes" : $"{pendientes} pendiente(s)",
            6.5,
            true,
            pendientes == 0
                ? new ColorPdf(0, 0, 0, 0.08, 0.40, 0.24)
                : new ColorPdf(0, 0, 0, 0.66, 0.12, 0.14));

        DibujarCuadricula(lienzo);
        foreach (var bloque in AgruparBloques(pagina.Sesiones))
            DibujarBloque(lienzo, bloque, pagina.MostrarGrupo);

        lienzo.Linea(Margen, 83, AnchoPagina - Margen, 83, 0.5, new ColorPdf(0, 0, 0, 0.82, 0.80, 0.78));
        lienzo.Texto(
            Margen,
            68,
            "Borrador generado el "
            + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            6.5,
            false,
            ColorPdf.Secundario);
        lienzo.TextoDerecha(
            AnchoPagina - Margen,
            68,
            $"Página {numeroPagina} de {totalPaginas}",
            6.5,
            false,
            ColorPdf.Secundario);
        lienzo.Texto(
            Margen,
            54,
            "Documento de revisión. La versión publicada en el sistema es la fuente oficial.",
            6,
            false,
            ColorPdf.Secundario);
        return lienzo.ObtenerContenido();
    }

    private static void DibujarCuadricula(LienzoPdf lienzo)
    {
        var anchoDia = (AnchoPagina - (2 * Margen) - AnchoHora) / Dias.Length;
        var alturaCuadricula = AltoEncabezadoDias + (8 * AltoFila);
        var baseCuadricula = TopeCuadricula - alturaCuadricula;
        lienzo.Rectangulo(Margen, baseCuadricula, AnchoHora, alturaCuadricula, new ColorPdf(0.95, 0.94, 0.93, 0, 0, 0));
        lienzo.Rectangulo(Margen + AnchoHora, TopeCuadricula - AltoEncabezadoDias,
            anchoDia * Dias.Length, AltoEncabezadoDias, new ColorPdf(0.95, 0.94, 0.93, 0, 0, 0));

        for (var dia = 0; dia < Dias.Length; dia++)
        {
            lienzo.TextoCentrado(
                Margen + AnchoHora + (dia * anchoDia),
                TopeCuadricula - 17,
                anchoDia,
                Dias[dia],
                6.8,
                true,
                ColorPdf.Texto);
        }

        for (var bloque = 1; bloque <= 8; bloque++)
        {
            var ySuperior = TopeCuadricula - AltoEncabezadoDias - ((bloque - 1) * AltoFila);
            lienzo.TextoCentrado(
                Margen,
                ySuperior - 31,
                AnchoHora,
                $"{bloque + 7:00}:00",
                6.5,
                true,
                ColorPdf.Texto);
            lienzo.TextoCentrado(
                Margen,
                ySuperior - 43,
                AnchoHora,
                $"{bloque + 8:00}:00",
                5.8,
                false,
                ColorPdf.Secundario);
        }

        var anchoTotal = AnchoHora + (anchoDia * Dias.Length);
        lienzo.Borde(Margen, baseCuadricula, anchoTotal, alturaCuadricula, 0.7, new ColorPdf(0, 0, 0, 0.78, 0.75, 0.72));
        lienzo.Linea(Margen, TopeCuadricula - AltoEncabezadoDias, Margen + anchoTotal,
            TopeCuadricula - AltoEncabezadoDias, 0.5, new ColorPdf(0, 0, 0, 0.82, 0.80, 0.78));
        for (var columna = 0; columna <= Dias.Length; columna++)
        {
            var x = Margen + AnchoHora + (columna * anchoDia);
            lienzo.Linea(x, baseCuadricula, x, TopeCuadricula, 0.45, new ColorPdf(0, 0, 0, 0.86, 0.84, 0.82));
        }
        for (var fila = 1; fila <= 8; fila++)
        {
            var y = TopeCuadricula - AltoEncabezadoDias - (fila * AltoFila);
            lienzo.Linea(Margen, y, Margen + anchoTotal, y, 0.45, new ColorPdf(0, 0, 0, 0.86, 0.84, 0.82));
        }
    }

    private static void DibujarBloque(
        LienzoPdf lienzo,
        BloquePdf bloque,
        bool mostrarGrupo)
    {
        if (bloque.Dia is < 1 or > 6 || bloque.BloqueInicio is < 1 or > 8)
            return;

        var anchoDia = (AnchoPagina - (2 * Margen) - AnchoHora) / Dias.Length;
        var x = Margen + AnchoHora + ((bloque.Dia - 1) * anchoDia) + 2.5;
        var ySuperior = TopeCuadricula - AltoEncabezadoDias - ((bloque.BloqueInicio - 1) * AltoFila);
        var alto = (Math.Min(bloque.Duracion, 9 - bloque.BloqueInicio) * AltoFila) - 5;
        var y = ySuperior - alto - 2.5;
        var ancho = anchoDia - 5;
        var color = Colores[IndiceColor(bloque.Sesion.MateriaClave)];
        lienzo.Rectangulo(x, y, ancho, alto, color);
        lienzo.Rectangulo(x, y, 3, alto, new ColorPdf(color.RTexto, color.GTexto, color.BTexto, 0, 0, 0));

        var cursor = y + alto - 11;
        lienzo.Texto(x + 6, cursor, $"{bloque.Sesion.HoraInicio}-{bloque.HoraFin}", 5.8, true, ColorPdf.Secundario);
        cursor -= 10;
        var lineasMateria = AjustarTexto(bloque.Sesion.Materia, 18, alto >= 140 ? 3 : 2);
        foreach (var linea in lineasMateria)
        {
            lienzo.Texto(x + 6, cursor, linea, 7.1, true, ColorPdf.Texto);
            cursor -= 8.5;
        }
        lienzo.Texto(x + 6, cursor, bloque.Sesion.MateriaClave, 5.8, false, ColorPdf.Secundario);
        cursor -= 8;
        var lineasContexto = mostrarGrupo
            ? new[]
            {
                $"Grupo {bloque.Sesion.Grupo}",
                CarreraCorta(bloque.Sesion.Carrera)
            }
            : AjustarTexto(bloque.Sesion.Docente, 21, 2);
        foreach (var linea in lineasContexto)
        {
            lienzo.Texto(x + 6, cursor, linea, 5.7, false, ColorPdf.Texto);
            cursor -= 7;
        }
        if (cursor > y + 15)
        {
            lienzo.Texto(x + 6, cursor, Acortar(bloque.Sesion.Espacio, 24), 5.5, false, ColorPdf.Secundario);
            cursor -= 7;
        }
        if (cursor > y + 7)
        {
            lienzo.Texto(
                x + 6,
                cursor,
                $"{bloque.Sesion.FechaInicio:dd/MM/yy}-{bloque.Sesion.FechaFin:dd/MM/yy}",
                5.2,
                false,
                ColorPdf.Secundario);
        }
    }

    private static BloquePdf[] AgruparBloques(HorarioSesionResumenDto[] sesiones)
    {
        var resultado = new List<BloquePdf>();
        foreach (var grupo in sesiones.GroupBy(x => new
                 {
                     x.CargaAcademicaId,
                     x.Dia,
                     x.Espacio,
                     x.FechaInicio,
                     x.FechaFin
                 }))
        {
            var ordenadas = grupo.OrderBy(x => x.Bloque).ToArray();
            var inicio = ordenadas[0];
            var duracion = 1;
            for (var indice = 1; indice < ordenadas.Length; indice++)
            {
                if (ordenadas[indice].Bloque == inicio.Bloque + duracion)
                {
                    duracion++;
                    continue;
                }

                resultado.Add(new BloquePdf(inicio.Dia, inicio.Bloque, duracion, inicio,
                    HoraFinal(inicio.Bloque, duracion)));
                inicio = ordenadas[indice];
                duracion = 1;
            }

            resultado.Add(new BloquePdf(inicio.Dia, inicio.Bloque, duracion, inicio,
                HoraFinal(inicio.Bloque, duracion)));
        }

        return resultado.OrderBy(x => x.Dia).ThenBy(x => x.BloqueInicio).ToArray();
    }

    private static string HoraFinal(byte bloqueInicio, int duracion) =>
        $"{bloqueInicio + duracion + 7:00}:00";

    private static int IndiceColor(string clave)
    {
        var valor = 17;
        foreach (var caracter in clave)
            valor = ((valor * 31) + caracter) & 0x7fffffff;
        return valor % Colores.Length;
    }

    private static string[] AjustarTexto(string texto, int maximoCaracteres, int maximoLineas)
    {
        var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lineas = new List<string>();
        var actual = string.Empty;
        foreach (var palabra in palabras)
        {
            var candidata = string.IsNullOrEmpty(actual) ? palabra : $"{actual} {palabra}";
            if (candidata.Length <= maximoCaracteres)
            {
                actual = candidata;
                continue;
            }

            if (!string.IsNullOrEmpty(actual))
                lineas.Add(actual);
            actual = palabra;
            if (lineas.Count == maximoLineas)
                break;
        }

        if (lineas.Count < maximoLineas && !string.IsNullOrEmpty(actual))
            lineas.Add(actual);
        if (lineas.Count == maximoLineas && palabras.Length > 0)
            lineas[^1] = Acortar(lineas[^1], maximoCaracteres);
        return lineas.ToArray();
    }

    private static string Acortar(string texto, int maximo) => texto.Length <= maximo
        ? texto
        : $"{texto[..Math.Max(1, maximo - 3)]}...";

    private static string CarreraCorta(string carrera)
    {
        var corta = carrera
            .Replace("Ingeniería en ", "Ing. ", StringComparison.OrdinalIgnoreCase)
            .Replace("Ingeniería ", "Ing. ", StringComparison.OrdinalIgnoreCase)
            .Replace("Licenciatura en ", "Lic. ", StringComparison.OrdinalIgnoreCase)
            .Replace("Licenciatura ", "Lic. ", StringComparison.OrdinalIgnoreCase);
        return Acortar(corta, 21);
    }

    private static byte[] CrearDocumento(byte[][] contenidos)
    {
        var objetos = new List<byte[]>();
        var primerObjetoPagina = 5;
        var referenciasPaginas = Enumerable.Range(0, contenidos.Length)
            .Select(x => primerObjetoPagina + (x * 2))
            .ToArray();
        objetos.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        objetos.Add(Ascii($"<< /Type /Pages /Count {contenidos.Length} /Kids [{string.Join(" ", referenciasPaginas.Select(x => $"{x} 0 R"))}] >>"));
        objetos.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objetos.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

        foreach (var contenido in contenidos)
        {
            var numeroContenido = objetos.Count + 2;
            objetos.Add(Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(AnchoPagina)} {N(AltoPagina)}] "
                + $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {numeroContenido} 0 R >>"));
            using var stream = new MemoryStream();
            var inicio = Ascii($"<< /Length {contenido.Length} >>\nstream\n");
            stream.Write(inicio);
            stream.Write(contenido);
            stream.Write(Ascii("\nendstream"));
            objetos.Add(stream.ToArray());
        }

        using var salida = new MemoryStream();
        salida.Write(Ascii("%PDF-1.4\n%âãÏÓ\n"));
        var posiciones = new List<long> { 0 };
        for (var indice = 0; indice < objetos.Count; indice++)
        {
            posiciones.Add(salida.Position);
            salida.Write(Ascii($"{indice + 1} 0 obj\n"));
            salida.Write(objetos[indice]);
            salida.Write(Ascii("\nendobj\n"));
        }

        var inicioXref = salida.Position;
        salida.Write(Ascii($"xref\n0 {objetos.Count + 1}\n"));
        salida.Write(Ascii("0000000000 65535 f \n"));
        foreach (var posicion in posiciones.Skip(1))
        {
            salida.Write(Ascii(
                posicion.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n"));
        }
        salida.Write(Ascii(
            $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n"
            + inicioXref.ToString(CultureInfo.InvariantCulture)
            + "\n%%EOF"));
        return salida.ToArray();
    }

    private static byte[] Ascii(string valor) => Encoding.Latin1.GetBytes(valor);
    private static string N(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed record PaginaPdf(
        string Titulo,
        string Subtitulo,
        bool MostrarGrupo,
        HorarioSesionResumenDto[] Sesiones);

    private sealed record BloquePdf(
        byte Dia,
        byte BloqueInicio,
        int Duracion,
        HorarioSesionResumenDto Sesion,
        string HoraFin);

    private readonly record struct ColorPdf(
        double R,
        double G,
        double B,
        double RTexto,
        double GTexto,
        double BTexto)
    {
        public static ColorPdf Texto => new(0, 0, 0, 0.08, 0.09, 0.11);
        public static ColorPdf Secundario => new(0, 0, 0, 0.35, 0.32, 0.31);
    }

    private sealed class LienzoPdf
    {
        private readonly StringBuilder contenido = new();

        public void Rectangulo(double x, double y, double ancho, double alto, ColorPdf color) =>
            contenido.Append(Cultura(
                $"{N(color.R)} {N(color.G)} {N(color.B)} rg {N(x)} {N(y)} {N(ancho)} {N(alto)} re f\n"));

        public void Borde(double x, double y, double ancho, double alto, double grosor, ColorPdf color) =>
            contenido.Append(Cultura(
                $"{N(color.RTexto)} {N(color.GTexto)} {N(color.BTexto)} RG {N(grosor)} w "
                + $"{N(x)} {N(y)} {N(ancho)} {N(alto)} re S\n"));

        public void Linea(double x1, double y1, double x2, double y2, double grosor, ColorPdf color) =>
            contenido.Append(Cultura(
                $"{N(color.RTexto)} {N(color.GTexto)} {N(color.BTexto)} RG {N(grosor)} w "
                + $"{N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S\n"));

        public void Texto(
            double x,
            double y,
            string texto,
            double tamano,
            bool negrita,
            ColorPdf color) => contenido.Append(Cultura(
                $"BT {N(color.RTexto)} {N(color.GTexto)} {N(color.BTexto)} rg "
                + $"/{(negrita ? "F2" : "F1")} {N(tamano)} Tf {N(x)} {N(y)} Td "
                + $"({Escapar(texto)}) Tj ET\n"));

        public void TextoCentrado(
            double x,
            double y,
            double ancho,
            string texto,
            double tamano,
            bool negrita,
            ColorPdf color)
        {
            var estimado = texto.Length * tamano * (negrita ? 0.56 : 0.50);
            Texto(x + Math.Max(2, (ancho - estimado) / 2), y, texto, tamano, negrita, color);
        }

        public void TextoDerecha(
            double derecha,
            double y,
            string texto,
            double tamano,
            bool negrita,
            ColorPdf color)
        {
            var estimado = texto.Length * tamano * (negrita ? 0.56 : 0.50);
            Texto(derecha - estimado, y, texto, tamano, negrita, color);
        }

        public byte[] ObtenerContenido() => Encoding.Latin1.GetBytes(contenido.ToString());

        private static string Escapar(string texto)
        {
            var normalizado = new string(texto.Select(x => x <= byte.MaxValue ? x : '?').ToArray());
            return normalizado
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
        }

        private static string Cultura(string valor) => valor;
    }
}
