using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

/// <summary>
/// Uma linha de dados da planilha já convertida. As células que não puderam ser lidas (data ou hora
/// inválida) vêm nulas e explicadas em <see cref="Errors"/>.
/// </summary>
/// <param name="RowNumber">Número da linha na planilha (a primeira linha de dados é a 2).</param>
public sealed record ShiftSheetRow(
    int RowNumber,
    string? Email,
    DateOnly? Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    IReadOnlyList<string> Errors);

public sealed record ShiftExportRow(string Email, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>
/// Leitura e escrita do .xlsx de turnos. Colunas: Email, Fecha, HoraInicio, HoraFin (as do enunciado),
/// as mesmas na importação e na exportação.
/// </summary>
public interface IShiftSpreadsheet
{
    /// <summary>
    /// Lê a primeira aba, localizando o cabeçalho pelo nome (sem diferenciar maiúsculas, em qualquer ordem).
    /// Linhas em branco são ignoradas. Falha se o arquivo não for um .xlsx válido ou faltarem colunas.
    /// </summary>
    Result<IReadOnlyList<ShiftSheetRow>> Read(Stream content);

    /// <summary>Gera o .xlsx com data e hora em células nativas do Excel.</summary>
    byte[] Write(IEnumerable<ShiftExportRow> rows);
}
