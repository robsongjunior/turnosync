using ClosedXML.Excel;

namespace TurnoSync.Tests.Support;

/// <summary>Gera planilhas .xlsx em memória para os testes de importação.</summary>
public static class SheetBuilder
{
    public static readonly string[] StandardHeaders = ["Email", "Fecha", "HoraInicio", "HoraFin"];

    /// <summary>
    /// Planilha com o cabeçalho padrão. Cada célula pode ser texto (string), valor nativo do Excel
    /// (DateTime, TimeSpan), número ou nulo (célula em branco).
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
