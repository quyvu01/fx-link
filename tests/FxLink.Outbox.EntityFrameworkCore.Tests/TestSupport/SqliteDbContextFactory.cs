using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FxLink.Outbox.EntityFrameworkCore.Tests.TestSupport;

// SQLite in-memory (not the EF Core InMemory provider) — a real relational engine that enforces
// concurrency tokens, which is exactly what the fencing tests below depend on. A single connection
// must stay open for the lifetime of the test since SQLite drops an in-memory database the moment
// its one and only connection closes; multiple TestOutboxDbContext instances can share that same
// connection to simulate separate scopes/instances racing against the same underlying store.
internal static class SqliteDbContextFactory
{
    internal static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    internal static TestOutboxDbContext CreateContext(SqliteConnection connection, bool ensureCreated = false)
    {
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>().UseSqlite(connection).Options;
        var context = new TestOutboxDbContext(options);
        if (ensureCreated) context.Database.EnsureCreated();
        return context;
    }
}
