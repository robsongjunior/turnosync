namespace TurnoSync.Application.Shifts;

public sealed record ImportedRow(int Row, string Email);

public sealed record FailedRow(int Row, string? Email, IReadOnlyList<string> Reasons);

/// <summary>Import report: the rows that were imported and the rows that failed, each with its reasons.</summary>
public sealed record ShiftImportReport(IReadOnlyList<ImportedRow> Imported, IReadOnlyList<FailedRow> Failed);
