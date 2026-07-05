using Flippo.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flippo.Tests.Data;

/// <summary>
/// Frische SQLite-Datei im Temp-Verzeichnis pro Test (kein InMemory-Provider — der lügt bei
/// FK/Cascade, Plan 4.1). Wendet die echte Migration an. Räumt Datei + WAL/SHM auf.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _dbPath;
    public IDbContextFactory<FlippoDbContext> Factory { get; }

    public SqliteTestDatabase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flippo-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<FlippoDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;
        Factory = new TestFactory(options);

        using var db = Factory.CreateDbContext();
        db.Database.Migrate();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* Test-Cleanup best effort */ }
        }
    }

    private sealed class TestFactory(DbContextOptions<FlippoDbContext> options)
        : IDbContextFactory<FlippoDbContext>
    {
        public FlippoDbContext CreateDbContext() => new(options);
    }
}
