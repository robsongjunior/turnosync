using Microsoft.EntityFrameworkCore;
using TurnoSync.Application.Common;
using TurnoSync.Application.Shifts;
using TurnoSync.Domain.Shifts;
using TurnoSync.Infra.Persistence;
using TurnoSync.Infra.Persistence.Repositories;
using TurnoSync.Infra.Spreadsheets;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Application;

/// <summary>
/// Import with spreadsheets generated in memory, the real ClosedXML adapter and an in-memory SQLite
/// created by the migrations. Seed collaborators: 1 Ana, 2 João, 3 Lucía, 4 Carlos, 5 Inês.
/// </summary>
public sealed class ShiftImportServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<Result<ShiftImportReport>> ImportAsync(byte[] file, string fileName = "shifts.xlsx")
    {
        await using var context = _database.CreateContext();
        var service = new ShiftImportService(
            new ClosedXmlShiftSpreadsheet(),
            new CollaboratorRepository(context),
            new ShiftRepository(context),
            new ShiftRequestValidator());

        using var stream = SheetBuilder.AsStream(file);
        return await service.ImportAsync(fileName, stream);
    }

    private async Task<ShiftImportReport> ImportOkAsync(byte[] file)
    {
        var result = await ImportAsync(file);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task SeedShiftAsync(int collaboratorId, DateOnly date, string start, string end, bool deleted = false)
    {
        await using var context = _database.CreateContext();
        var shift = Shift.Create(collaboratorId, date, TimeOnly.Parse(start), TimeOnly.Parse(end));
        if (deleted)
        {
            shift.Delete();
        }

        context.Shifts.Add(shift);
        await context.SaveChangesAsync();
    }

    private async Task<List<(int CollaboratorId, DateOnly Date, TimeOnly Start, TimeOnly End)>> StoredShiftsAsync()
    {
        await using var context = _database.CreateContext();
        var shifts = await context.Shifts.OrderBy(s => s.StartsAt).ThenBy(s => s.CollaboratorId).ToListAsync();
        return shifts.Select(s => (s.CollaboratorId, s.Date, s.StartTime, s.EndTime)).ToList();
    }

    [Fact]
    public async Task Import_ShouldImportValidRowsAndReportEachFailureWithItsReason()
    {
        await SeedShiftAsync(1, new DateOnly(2026, 10, 12), "08:00", "16:00");
        var file = SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-13", "08:00", "16:00"],      // 2  ok
            ["ana@turnosync.dev", "2026-10-12", "12:00", "20:00"],      // 3  overlaps a shift that already exists in the database
            ["ana@turnosync.dev", "2026-10-13", "12:00", "18:00"],      // 4  overlaps row 2 of the same file
            ["ana@turnosync.dev", "2026-10-13", "16:00", "20:00"],      // 5  touches row 2: allowed
            ["joao@turnosync.dev", "2026-10-13", "08:00", "16:00"],     // 6  same time, different collaborator: allowed
            ["nobody@turnosync.dev", "2026-10-13", "08:00", "16:00"],   // 7  unknown collaborator
            ["ana@turnosync.dev", "13/10/2026", "08:00", "16:00"],      // 8  unreadable date
            ["ana@turnosync.dev", "2026-10-14", "08:00", "08:00"],      // 9  start = end
            [null, "2026-10-14", "08:00", "16:00"],                     // 10 no email
            ["ana@turnosync.dev", "2026-10-14", "25:00", "16:00"],      // 11 unreadable time
            ["ana@turnosync.dev", "2026-10-15", null, "16:00"]);        // 12 blank start time

        var report = await ImportOkAsync(file);

        Assert.Equal([(2, "ana@turnosync.dev"), (5, "ana@turnosync.dev"), (6, "joao@turnosync.dev")],
            report.Imported.Select(i => (i.Row, i.Email)));
        Assert.Equal([3, 4, 7, 8, 9, 10, 11, 12], report.Failed.Select(f => f.Row));

        var failed = report.Failed.ToDictionary(f => f.Row);
        Assert.Equal(["Overlaps an existing shift (2026-10-12 08:00-16:00)."], failed[3].Reasons);
        Assert.Equal(["Overlaps row 2."], failed[4].Reasons);
        Assert.Equal(["No collaborator found with the email 'nobody@turnosync.dev'."], failed[7].Reasons);
        Assert.Single(failed[8].Reasons);
        Assert.StartsWith("Fecha: '13/10/2026'", failed[8].Reasons[0]);
        Assert.Equal(["'End Time' must be different from 'Start Time'."], failed[9].Reasons);
        Assert.Equal(["Email is required."], failed[10].Reasons);
        Assert.StartsWith("HoraInicio: '25:00'", failed[11].Reasons.Single());
        Assert.Equal(["'Start Time' must not be empty."], failed[12].Reasons);
        Assert.Null(failed[10].Email);
        Assert.Equal("ana@turnosync.dev", failed[3].Email);

        // Only the accepted rows were saved (plus the shift that already existed).
        Assert.Equal(
            [
                (1, new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 0)),
                (1, new DateOnly(2026, 10, 13), new TimeOnly(8, 0), new TimeOnly(16, 0)),
                (2, new DateOnly(2026, 10, 13), new TimeOnly(8, 0), new TimeOnly(16, 0)),
                (1, new DateOnly(2026, 10, 13), new TimeOnly(16, 0), new TimeOnly(20, 0)),
            ],
            await StoredShiftsAsync());
    }

    [Fact]
    public async Task Import_ShouldReportAllReasons_WhenARowHasMoreThanOneProblem()
    {
        var report = await ImportOkAsync(SheetBuilder.Standard(
            ["nobody@turnosync.dev", "2026-10-13", "08:00", "08:00"]));

        var failed = Assert.Single(report.Failed);
        Assert.Equal(
            ["No collaborator found with the email 'nobody@turnosync.dev'.", "'End Time' must be different from 'Start Time'."],
            failed.Reasons);
    }

    [Fact]
    public async Task Import_ShouldMatchCollaboratorsByEmailIgnoringCaseAndSpaces()
    {
        var report = await ImportOkAsync(SheetBuilder.Standard(
            ["  ANA@TurnoSync.DEV ", "2026-10-13", "08:00", "16:00"]));

        Assert.Single(report.Imported);
        Assert.Empty(report.Failed);
    }

    [Fact]
    public async Task Import_ShouldRejectTheSecondRow_WhenTheSameRowAppearsTwice()
    {
        var report = await ImportOkAsync(SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-13", "08:00", "16:00"],
            ["ana@turnosync.dev", "2026-10-13", "08:00", "16:00"]));

        Assert.Equal([2], report.Imported.Select(i => i.Row));
        Assert.Equal(["Overlaps row 2."], Assert.Single(report.Failed).Reasons);
        Assert.Single(await StoredShiftsAsync());
    }

    [Fact]
    public async Task Import_ShouldHandleNightShiftsAcrossDaysBothAgainstTheDatabaseAndTheFile()
    {
        await SeedShiftAsync(1, new DateOnly(2026, 10, 12), "22:00", "06:00");
        var file = SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-13", "05:00", "09:00"],  // 2 reaches into the end of the overnight shift in the database
            ["ana@turnosync.dev", "2026-10-13", "06:00", "10:00"],  // 3 starts when the overnight shift ends: allowed
            ["ana@turnosync.dev", "2026-10-13", "22:00", "06:00"],  // 4 overnight shift of the 13th: allowed
            ["ana@turnosync.dev", "2026-10-14", "02:00", "04:00"]); // 5 falls inside the overnight shift of row 4

        var report = await ImportOkAsync(file);

        Assert.Equal([3, 4], report.Imported.Select(i => i.Row));
        Assert.Equal([2, 5], report.Failed.Select(f => f.Row));
        Assert.Equal(["Overlaps row 4."], report.Failed.Single(f => f.Row == 5).Reasons);
    }

    [Fact]
    public async Task Import_ShouldNotBeBlockedByDeletedShifts()
    {
        await SeedShiftAsync(1, new DateOnly(2026, 10, 13), "08:00", "16:00", deleted: true);

        var report = await ImportOkAsync(SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-13", "08:00", "16:00"]));

        Assert.Single(report.Imported);
    }

    [Fact]
    public async Task Import_ShouldReturnEmptyReport_WhenThereAreNoDataRows()
    {
        var report = await ImportOkAsync(SheetBuilder.Standard());

        Assert.Empty(report.Imported);
        Assert.Empty(report.Failed);
    }

    [Fact]
    public async Task Import_ShouldImportNothing_WhenEveryRowFails()
    {
        var report = await ImportOkAsync(SheetBuilder.Standard(
            ["nobody@turnosync.dev", "2026-10-13", "08:00", "16:00"],
            ["ana@turnosync.dev", "oops", "08:00", "16:00"]));

        Assert.Empty(report.Imported);
        Assert.Equal(2, report.Failed.Count);
        Assert.Empty(await StoredShiftsAsync());
    }

    [Theory]
    [InlineData("shifts.csv")]
    [InlineData("shifts.xls")]
    [InlineData("shifts")]
    public async Task Import_ShouldReturnValidationError_WhenFileNameIsNotXlsx(string fileName)
    {
        var result = await ImportAsync(SheetBuilder.Standard(), fileName);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("file", result.Error.FieldErrors.Keys);
    }

    [Fact]
    public async Task Import_ShouldAcceptUpperCaseExtension()
    {
        var result = await ImportAsync(SheetBuilder.Standard(), "SHIFTS.XLSX");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Import_ShouldReturnValidationError_WhenContentIsNotAWorkbookOrColumnsAreMissing()
    {
        var notExcel = await ImportAsync("plain text"u8.ToArray());
        var missingColumns = await ImportAsync(SheetBuilder.Build(sheet => SheetBuilder.WriteRow(sheet, 1, ["Email", "Fecha"])));

        Assert.Equal(ErrorType.Validation, notExcel.Error!.Type);
        Assert.Equal(ErrorType.Validation, missingColumns.Error!.Type);
        Assert.Empty(await StoredShiftsAsync());
    }
}
