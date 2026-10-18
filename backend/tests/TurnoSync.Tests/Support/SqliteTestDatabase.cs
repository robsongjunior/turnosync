using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TurnoSync.Infra.Persistence;

namespace TurnoSync.Tests.Support;

/// <summary>
/// Banco SQLite em memória com nome único por teste, criado com as migrations reais (inclui o seed).
/// A conexão keep-alive mantém o banco vivo enquanto o teste usa vários DbContext.
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
