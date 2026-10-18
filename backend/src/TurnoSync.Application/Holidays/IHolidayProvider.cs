namespace TurnoSync.Application.Holidays;

public interface IHolidayProvider
{
    /// <summary>
    /// Datas dos feriados nacionais do país no ano. País não suportado devolve um conjunto vazio.
    /// Devolve <c>null</c> quando a consulta falha (rede, timeout, 5xx): "desconhecido", que é diferente de "sem feriados".
    /// Nunca lança por falha da API externa.
    /// </summary>
    Task<IReadOnlySet<DateOnly>?> GetNationalHolidaysAsync(
        string countryCode, int year, CancellationToken ct = default);
}
