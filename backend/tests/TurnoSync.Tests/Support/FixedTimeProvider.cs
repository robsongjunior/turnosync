namespace TurnoSync.Tests.Support;

/// <summary>
/// Reloj fijo para las pruebas. Por defecto es el 1 de octubre de 2026 a las 08:00 en Madrid, anterior a todas las
/// fechas de turno que usan las pruebas, así que esos turnos son futuros sea cual sea el día en que se ejecuten.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(2));

    public FixedTimeProvider() : this(DefaultNow)
    {
    }

    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
