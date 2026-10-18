using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TurnoSync.Infra.Persistence;

namespace TurnoSync.Tests.Support;

/// <summary>
/// In-memory SQLite database with a unique name per test, created with the real migrations (including the seed).
/// The keep-alive connection keeps the database alive while the test uses several DbContexts.
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
