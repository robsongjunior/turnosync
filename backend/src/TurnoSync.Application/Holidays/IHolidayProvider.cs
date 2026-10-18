namespace TurnoSync.Application.Holidays;

public interface IHolidayProvider
{
    /// <summary>
    /// Fechas de los festivos nacionales del país en el año indicado. Un país no admitido devuelve un conjunto vacío.
    /// Devuelve <c>null</c> cuando la consulta falla (red, timeout, 5xx): "desconocido", que es distinto de "sin festivos".
    /// Nunca lanza excepciones por un fallo de la API externa.
    /// </summary>
    Task<IReadOnlySet<DateOnly>?> GetNationalHolidaysAsync(
        string countryCode, int year, CancellationToken ct = default);
}
