namespace TurnoSync.Application.Shifts;

public sealed record ImportedRow(int Row, string Email);

public sealed record FailedRow(int Row, string? Email, IReadOnlyList<string> Reasons);

/// <summary>Informe da importação: linhas importadas e linhas que falharam, cada uma com seus motivos.</summary>
public sealed record ShiftImportReport(IReadOnlyList<ImportedRow> Imported, IReadOnlyList<FailedRow> Failed);
