using ClosedXML.Excel;

namespace TurnoSync.Tests.Support;

/// <summary>Genera hojas de cálculo .xlsx en memoria para las pruebas de importación.</summary>
public static class SheetBuilder
{
    public static readonly string[] StandardHeaders = ["Email", "Fecha", "HoraInicio", "HoraFin"];

    /// <summary>
    /// Una hoja con el encabezado estándar. Cada celda puede ser texto (string), un valor nativo de Excel
    /// (DateTime, TimeSpan), un número o null (celda en blanco).
    /// </summary>
    public static byte[] Standard(params object?[][] rows) => Build(sheet =>
    {
        WriteRow(sheet, 1, StandardHeaders);
        for (var i = 0; i < rows.Length; i++)
        {
            WriteRow(sheet, i + 2, rows[i]);
        }
    });

    public static byte[] Build(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.Worksheets.Add("Shifts"));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static void WriteRow(IXLWorksheet sheet, int rowNumber, IReadOnlyList<object?> values)
    {
        for (var column = 0; column < values.Count; column++)
        {
            if (values[column] is { } value)
            {
                sheet.Cell(rowNumber, column + 1).Value = XLCellValue.FromObject(value);
            }
        }
    }

    public static MemoryStream AsStream(byte[] content) => new(content);
}
