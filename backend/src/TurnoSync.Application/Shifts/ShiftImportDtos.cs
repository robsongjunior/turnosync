namespace TurnoSync.Application.Shifts;

public sealed record ImportedRow(int Row, string Email);

public sealed record FailedRow(int Row, string? Email, IReadOnlyList<string> Reasons);

/// <summary>Informe de importación: las filas importadas y las que fallaron, cada una con sus motivos.</summary>
public sealed record ShiftImportReport(IReadOnlyList<ImportedRow> Imported, IReadOnlyList<FailedRow> Failed);
