using ClosedXML.Excel;
using TurnoSync.Application.Common;
using TurnoSync.Application.Shifts;
using TurnoSync.Infra.Spreadsheets;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Infra;

public sealed class ClosedXmlShiftSpreadsheetTests
{
    private readonly ClosedXmlShiftSpreadsheet _spreadsheet = new();

    private IReadOnlyList<ShiftSheetRow> Read(byte[] file)
    {
        using var stream = SheetBuilder.AsStream(file);
        var result = _spreadsheet.Read(stream);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private ResultError ReadFailure(byte[] file)
    {
        using var stream = SheetBuilder.AsStream(file);
        var result = _spreadsheet.Read(stream);
        Assert.False(result.IsSuccess);
        return result.Error!;
    }

    [Fact]
    public void Write_ShouldProduceNativeDateAndTimeCellsWithTheExpectedFormats()
    {
        var file = _spreadsheet.Write([new ShiftExportRow("ana@turnosync.dev", new DateOnly(2026, 10, 12), new TimeOnly(22, 0), new TimeOnly(6, 0))]);

        using var workbook = new XLWorkbook(SheetBuilder.AsStream(file));
        var sheet = workbook.Worksheet(1);

        Assert.Equal(["Email", "Fecha", "HoraInicio", "HoraFin"], sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.True(sheet.Cell("B2").Value.IsDateTime || sheet.Cell("B2").Value.IsNumber);
        Assert.False(sheet.Cell("B2").Value.IsText);
        Assert.False(sheet.Cell("C2").Value.IsText);
        Assert.False(sheet.Cell("D2").Value.IsText);
        Assert.Equal("yyyy-mm-dd", sheet.Cell("B2").Style.DateFormat.Format);
        Assert.Equal("hh:mm", sheet.Cell("C2").Style.DateFormat.Format);
        Assert.Equal("hh:mm", sheet.Cell("D2").Style.DateFormat.Format);
    }

    [Fact]
    public void WriteThenRead_ShouldRoundTripEveryRowExactly()
    {
        ShiftExportRow[] exported =
        [
            new("ana@turnosync.dev", new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 0)),
            new("joao@turnosync.dev", new DateOnly(2026, 12, 31), new TimeOnly(22, 0), new TimeOnly(6, 0)),
            new("ines@turnosync.dev", new DateOnly(2027, 1, 1), new TimeOnly(0, 0), new TimeOnly(7, 59)),
            new("carlos@turnosync.dev", new DateOnly(2026, 2, 28), new TimeOnly(13, 7), new TimeOnly(23, 59)),
        ];

        var rows = Read(_spreadsheet.Write(exported));

        Assert.Equal(
            exported.Select(e => ((string?)e.Email, (DateOnly?)e.Date, (TimeOnly?)e.StartTime, (TimeOnly?)e.EndTime)),
            rows.Select(r => (r.Email, r.Date, r.StartTime, r.EndTime)));
        Assert.All(rows, r => Assert.Empty(r.Errors));
        Assert.Equal([2, 3, 4, 5], rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Write_ShouldProduceOnlyTheHeader_WhenThereAreNoRows()
    {
        var file = _spreadsheet.Write([]);

        Assert.Empty(Read(file));
    }

    [Fact]
    public void Read_ShouldAcceptTextDatesAndTimes()
    {
        var rows = Read(SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-12", "08:00", "16:00"],
            ["joao@turnosync.dev", "2026-10-13", "8:30", "17:15:45"]));

        Assert.Equal(new DateOnly(2026, 10, 12), rows[0].Date);
        Assert.Equal(new TimeOnly(8, 0), rows[0].StartTime);
        Assert.Equal(new TimeOnly(8, 30), rows[1].StartTime);
        Assert.Equal(new TimeOnly(17, 15), rows[1].EndTime); // segundos descartados
    }

    [Fact]
    public void Read_ShouldAcceptNativeExcelDatesAndTimes()
    {
        var rows = Read(SheetBuilder.Standard(
            ["ana@turnosync.dev", new DateTime(2026, 10, 12), new TimeSpan(8, 0, 0), new TimeSpan(16, 30, 0)],
            ["joao@turnosync.dev", new DateTime(2026, 10, 13, 15, 45, 0), new DateTime(2026, 10, 13, 9, 15, 0), new DateTime(2026, 10, 13, 17, 0, 0)]));

        Assert.Equal((new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 30)), (rows[0].Date!.Value, rows[0].StartTime!.Value, rows[0].EndTime!.Value));
        Assert.Equal(new DateOnly(2026, 10, 13), rows[1].Date); // la parte horaria de la fecha se ignora
        Assert.Equal(new TimeOnly(9, 15), rows[1].StartTime);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(7, 59)]
    [InlineData(13, 7)]
    [InlineData(22, 45)]
    [InlineData(0, 1)]
    [InlineData(23, 59)]
    public void Read_ShouldNotLoseAMinute_WhenTimeIsAFractionOfTheDay(int hour, int minute)
    {
        // Así guarda Excel la hora: las fracciones binarias de 1/3, etc. traen ruido de coma flotante.
        var fraction = (hour * 60 + minute) / 1440.0;
        var file = SheetBuilder.Standard(["ana@turnosync.dev", new DateOnly(2026, 10, 12).ToDateTime(TimeOnly.MinValue).ToOADate(), fraction, fraction]);

        var row = Read(file).Single();

        Assert.Equal(new DateOnly(2026, 10, 12), row.Date);
        Assert.Equal(new TimeOnly(hour, minute), row.StartTime);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void Read_ShouldFindHeadersByNameIgnoringCaseWhitespaceOrderAndExtraColumns()
    {
        var file = SheetBuilder.Build(sheet =>
        {
            SheetBuilder.WriteRow(sheet, 1, ["Notes", " horafin ", "FECHA", "email", "HoraInicio"]);
            SheetBuilder.WriteRow(sheet, 2, ["ignored", "16:00", "2026-10-12", "ana@turnosync.dev", "08:00"]);
        });

        var row = Read(file).Single();

        Assert.Equal(
            ("ana@turnosync.dev", new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 0)),
            (row.Email, row.Date!.Value, row.StartTime!.Value, row.EndTime!.Value));
    }

    [Fact]
    public void Read_ShouldSkipBlankRowsButKeepSheetRowNumbers()
    {
        var rows = Read(SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-12", "08:00", "16:00"],
            [null, null, null, null],
            ["joao@turnosync.dev", "2026-10-13", "08:00", "16:00"]));

        Assert.Equal([2, 4], rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Read_ShouldReturnEmpty_WhenThereIsOnlyTheHeader() =>
        Assert.Empty(Read(SheetBuilder.Standard()));

    [Fact]
    public void Read_ShouldReadOnlyTheFirstSheet()
    {
        var file = SheetBuilder.Build(first =>
        {
            SheetBuilder.WriteRow(first, 1, SheetBuilder.StandardHeaders);
            SheetBuilder.WriteRow(first, 2, ["ana@turnosync.dev", "2026-10-12", "08:00", "16:00"]);
            var second = first.Workbook.Worksheets.Add("Other");
            SheetBuilder.WriteRow(second, 1, SheetBuilder.StandardHeaders);
            SheetBuilder.WriteRow(second, 2, ["joao@turnosync.dev", "2026-10-13", "08:00", "16:00"]);
        });

        Assert.Equal(["ana@turnosync.dev"], Read(file).Select(r => r.Email));
    }

    [Fact]
    public void Read_ShouldReportUnreadableCellsAsRowErrorsWithoutThrowing()
    {
        var rows = Read(SheetBuilder.Standard(
            ["ana@turnosync.dev", "13/10/2026", "8am", "16:00"],
            ["joao@turnosync.dev", true, "08:00", "not a time"]));

        Assert.Null(rows[0].Date);
        Assert.Null(rows[0].StartTime);
        Assert.Equal(new TimeOnly(16, 0), rows[0].EndTime);
        Assert.Equal(
            [
                "Fecha: '13/10/2026' no es una fecha válida (usa una celda de fecha o el formato yyyy-MM-dd).",
                "HoraInicio: '8am' no es una hora válida (usa una celda de hora o el formato HH:mm).",
            ],
            rows[0].Errors);
        Assert.Contains(rows[1].Errors, e => e.StartsWith("Fecha", StringComparison.Ordinal));
        Assert.Contains(rows[1].Errors, e => e.StartsWith("HoraFin", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_ShouldKeepBlankCellsAsNullWithoutErrors_SoTheValidatorReportsThem()
    {
        var row = Read(SheetBuilder.Standard(["ana@turnosync.dev", "2026-10-12", null, "16:00"])).Single();

        Assert.Null(row.StartTime);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void Read_ShouldFail_WhenColumnsAreMissing()
    {
        var file = SheetBuilder.Build(sheet => SheetBuilder.WriteRow(sheet, 1, ["Email", "Fecha", "HoraInicio"]));

        var error = ReadFailure(file);

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(
            "La hoja de cálculo debe tener las columnas Email, Fecha, HoraInicio, HoraFin (faltan: HoraFin).",
            error.FieldErrors["file"][0]);
    }

    [Fact]
    public void Read_ShouldFail_WhenSheetIsEmpty()
    {
        var error = ReadFailure(SheetBuilder.Build(_ => { }));

        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("this is not a spreadsheet")]
    public void Read_ShouldFail_WhenFileIsNotAnXlsx(string content)
    {
        var error = ReadFailure(System.Text.Encoding.UTF8.GetBytes(content));

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("El archivo no es un libro de Excel (.xlsx) válido.", error.FieldErrors["file"][0]);
    }

    [Fact]
    public void Read_ShouldFail_WhenFileIsAZipThatIsNotAWorkbook()
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("hello.txt");
        }

        var error = ReadFailure(stream.ToArray());

        Assert.Equal(ErrorType.Validation, error.Type);
    }
}
