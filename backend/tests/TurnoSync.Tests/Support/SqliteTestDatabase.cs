using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TurnoSync.Infra.Persistence;

namespace TurnoSync.Tests.Support;

/// <summary>
/// Base de datos SQLite en memoria con nombre único por prueba, creada con las migraciones reales (incluidos los datos iniciales).
/// La conexión que se mantiene abierta mantiene viva la base mientras la prueba usa varios DbContext.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<TurnoSyncContext> _options;

    public SqliteTestDatabase()
    {
        ConnectionString = $"Data Source=TurnoSyncTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        _options = new DbContextOptionsBuilder<TurnoSyncContext>()
            .UseSqlite(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public string ConnectionString { get; }

    public TurnoSyncContext CreateContext() => new(_options);

    public void Dispose() => _keepAlive.Dispose();
}
