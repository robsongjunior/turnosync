using System.Globalization;
using ClosedXML.Excel;
using TurnoSync.Application.Common;
using TurnoSync.Application.Shifts;

namespace TurnoSync.Infra.Spreadsheets;

public sealed class ClosedXmlShiftSpreadsheet : IShiftSpreadsheet
{
    private const string EmailHeader = "Email";
    private const string DateHeader = "Fecha";
    private const string StartTimeHeader = "HoraInicio";
    private const string EndTimeHeader = "HoraFin";

    private static readonly string[] Headers = [EmailHeader, DateHeader, StartTimeHeader, EndTimeHeader];

    private static readonly string[] TextTimeFormats = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"];

    public Result<IReadOnlyList<ShiftSheetRow>> Read(Stream content)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception)
        {
            // Any failure to open it (not a zip, not OOXML, corrupt) means "not a valid .xlsx".
            return ResultError.Validation("file", "The file is not a valid .xlsx workbook.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            var headerRow = sheet?.FirstRowUsed();
            var columns = headerRow is null ? [] : MapColumns(headerRow);

            var missing = Headers.Where(h => !columns.ContainsKey(h)).ToList();
            if (sheet is null || headerRow is null || missing.Count > 0)
            {
                return ResultError.Validation(
                    "file",
                    $"The spreadsheet must have the columns {string.Join(", ", Headers)} " +
                    $"(missing: {string.Join(", ", sheet is null || headerRow is null ? Headers : missing)}).");
            }

            var rows = new List<ShiftSheetRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

            for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                var cells = Headers.ToDictionary(h => h, h => sheet.Cell(rowNumber, columns[h]));
                if (cells.Values.All(c => c.IsEmpty()))
                {
                    continue;
                }

                rows.Add(ReadRow(rowNumber, cells));
            }

            return rows;
        }
    }

    public byte[] Write(IEnumerable<ShiftExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Shifts");

        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }

        sheet.Row(1).Style.Font.Bold = true;

        var rowNumber = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.Email;

            // Native date and time cells (not text), so the file can be imported again.
            var date = sheet.Cell(rowNumber, 2);
            date.Value = row.Date.ToDateTime(TimeOnly.MinValue);
            date.Style.DateFormat.Format = "yyyy-mm-dd";

            var start = sheet.Cell(rowNumber, 3);
            start.Value = row.StartTime.ToTimeSpan();
            start.Style.DateFormat.Format = "hh:mm";

            var end = sheet.Cell(rowNumber, 4);
            end.Value = row.EndTime.ToTimeSpan();
            end.Style.DateFormat.Format = "hh:mm";

            rowNumber++;
        }

        // Fixed widths: AdjustToContents measures fonts, which is unreliable in containers without installed fonts.
        sheet.Column(1).Width = 32;
        sheet.Column(2).Width = 12;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 12;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Dictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            columns.TryAdd(cell.GetString().Trim(), cell.Address.ColumnNumber);
        }

        return columns;
    }

    private static ShiftSheetRow ReadRow(int rowNumber, Dictionary<string, IXLCell> cells)
    {
        var errors = new List<string>();

        var email = cells[EmailHeader].GetString().Trim();
        var date = ReadDate(cells[DateHeader], errors);
        var start = ReadTime(cells[StartTimeHeader], StartTimeHeader, errors);
        var end = ReadTime(cells[EndTimeHeader], EndTimeHeader, errors);

        return new ShiftSheetRow(rowNumber, email.Length == 0 ? null : email, date, start, end, errors);
    }

    /// <summary>Date as a native Excel value (or serial number) or as yyyy-MM-dd text. An empty cell = null, no error.</summary>
    private static DateOnly? ReadDate(IXLCell cell, List<string> errors)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        var value = cell.Value;

        if (value.IsDateTime)
        {
            return DateOnly.FromDateTime(value.GetDateTime());
        }

        if (value.IsNumber && value.GetNumber() is >= 1 and < 2_958_466)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(value.GetNumber())));
        }

        if (value.IsText
            && DateOnly.TryParseExact(value.GetText().Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{DateHeader}: '{cell.GetString()}' is not a valid date (use a date cell or yyyy-MM-dd).");
        return null;
    }

    /// <summary>Time as a native Excel value (fraction of a day) or as HH:mm text. Seconds are discarded.</summary>
    private static TimeOnly? ReadTime(IXLCell cell, string header, List<string> errors)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        var value = cell.Value;

        if (value.IsTimeSpan)
        {
            return FromMinutes(value.GetTimeSpan().TotalMinutes);
        }

        if (value.IsDateTime)
        {
            return FromMinutes(value.GetDateTime().TimeOfDay.TotalMinutes);
        }

        if (value.IsNumber)
        {
            var number = value.GetNumber();
            return FromMinutes((number - Math.Floor(number)) * TimeSpan.FromDays(1).TotalMinutes);
        }

        if (value.IsText
            && TimeOnly.TryParseExact(value.GetText().Trim(), TextTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return new TimeOnly(parsed.Hour, parsed.Minute);
        }

        errors.Add($"{header}: '{cell.GetString()}' is not a valid time (use a time cell or HH:mm).");
        return null;
    }

    /// <summary>
    /// Excel stores time as a fraction of a day, which brings floating-point noise (07:59:59.9999 instead
    /// of 08:00). Rounding to the nearest minute avoids reading one minute too few.
    /// </summary>
    private static TimeOnly FromMinutes(double totalMinutes)
    {
        var minutes = (int)Math.Round(totalMinutes) % (24 * 60);
        return new TimeOnly(minutes / 60, minutes % 60);
    }
}
