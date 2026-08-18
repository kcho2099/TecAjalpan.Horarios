using System.IO.Compression;
using System.Text;
using System.Xml;
using TecAjalpan.Horarios.Contracts.Horarios;

namespace TecAjalpan.Horarios.Web.Services;

internal static class ExportadorHorarioExcel
{
    private static readonly string[] Encabezados =
    [
        "Carrera", "Modalidad", "Grupo", "Día", "Horario", "Clave",
        "Materia", "Docente", "Espacio", "Vigencia", "Sesiones"
    ];

    public static byte[] Crear(
        string periodo,
        int numeroVersion,
        HorarioSesionResumenDto[] sesiones)
    {
        using var salida = new MemoryStream();
        using (var paquete = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true))
        {
            EscribirTexto(paquete, "[Content_Types].xml", Contenidos);
            EscribirTexto(paquete, "_rels/.rels", RelacionesRaiz);
            EscribirTexto(paquete, "xl/workbook.xml", Libro);
            EscribirTexto(paquete, "xl/_rels/workbook.xml.rels", RelacionesLibro);
            EscribirTexto(paquete, "xl/styles.xml", Estilos);
            EscribirHoja(paquete, periodo, numeroVersion, sesiones);
        }

        return salida.ToArray();
    }

    private static void EscribirHoja(
        ZipArchive paquete,
        string periodo,
        int numeroVersion,
        HorarioSesionResumenDto[] sesiones)
    {
        var entrada = paquete.CreateEntry(
            "xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        using var stream = entrada.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false
        });
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var ultimaFila = sesiones.Length + 4;

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", ns);
        writer.WriteStartElement("dimension", ns);
        writer.WriteAttributeString("ref", $"A1:K{ultimaFila}");
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews", ns);
        writer.WriteStartElement("sheetView", ns);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", ns);
        writer.WriteAttributeString("ySplit", "4");
        writer.WriteAttributeString("topLeftCell", "A5");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        EscribirColumnas(writer, ns);
        writer.WriteStartElement("sheetData", ns);
        EscribirFila(writer, ns, 1, [$"Horario generado · Versión {numeroVersion}"], 2);
        EscribirFila(writer, ns, 2, [$"Periodo: {periodo}"], 0);
        EscribirFila(writer, ns, 4, Encabezados, 1);

        var fila = 5;
        foreach (var sesion in sesiones)
        {
            EscribirFila(writer, ns, fila,
            [
                sesion.Carrera,
                sesion.Modalidad,
                sesion.Grupo,
                sesion.DiaTexto,
                $"{sesion.HoraInicio}–{sesion.HoraFin}",
                sesion.MateriaClave,
                sesion.Materia,
                sesion.Docente,
                sesion.Espacio,
                $"{sesion.FechaInicio:dd/MM/yyyy}–{sesion.FechaFin:dd/MM/yyyy}",
                sesion.NumeroSesiones.ToString()
            ], 0);
            fila++;
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", ns);
        writer.WriteAttributeString("ref", $"A4:K{ultimaFila}");
        writer.WriteEndElement();
        writer.WriteStartElement("mergeCells", ns);
        writer.WriteAttributeString("count", "2");
        EscribirCeldaCombinada(writer, ns, "A1:K1");
        EscribirCeldaCombinada(writer, ns, "A2:K2");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void EscribirColumnas(XmlWriter writer, string ns)
    {
        double[] anchos = [24, 16, 12, 12, 15, 14, 34, 30, 26, 24, 11];
        writer.WriteStartElement("cols", ns);
        for (var indice = 0; indice < anchos.Length; indice++)
        {
            writer.WriteStartElement("col", ns);
            writer.WriteAttributeString("min", (indice + 1).ToString());
            writer.WriteAttributeString("max", (indice + 1).ToString());
            writer.WriteAttributeString("width", anchos[indice].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void EscribirFila(
        XmlWriter writer,
        string ns,
        int numero,
        string[] valores,
        int estilo)
    {
        writer.WriteStartElement("row", ns);
        writer.WriteAttributeString("r", numero.ToString());
        for (var indice = 0; indice < valores.Length; indice++)
        {
            writer.WriteStartElement("c", ns);
            writer.WriteAttributeString("r", $"{NombreColumna(indice + 1)}{numero}");
            writer.WriteAttributeString("s", estilo.ToString());
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", ns);
            writer.WriteElementString("t", ns, valores[indice] ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string NombreColumna(int numero)
    {
        var nombre = string.Empty;
        while (numero > 0)
        {
            numero--;
            nombre = (char)('A' + numero % 26) + nombre;
            numero /= 26;
        }
        return nombre;
    }

    private static void EscribirCeldaCombinada(
        XmlWriter writer,
        string ns,
        string referencia)
    {
        writer.WriteStartElement("mergeCell", ns);
        writer.WriteAttributeString("ref", referencia);
        writer.WriteEndElement();
    }

    private static void EscribirTexto(
        ZipArchive paquete,
        string ruta,
        string contenido)
    {
        var entrada = paquete.CreateEntry(ruta, CompressionLevel.Fastest);
        using var stream = entrada.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contenido);
    }

    private const string Contenidos = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string RelacionesRaiz = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Libro = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Horario" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string RelacionesLibro = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string Estilos = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="3">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font>
            <font><b/><color rgb="FF822427"/><sz val="16"/><name val="Calibri"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF822427"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="3">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
            <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0"/>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
