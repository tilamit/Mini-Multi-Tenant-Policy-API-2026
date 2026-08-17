using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PolicyApi.Data;
using PolicyApi.Tenancy;
using PolicyApi.Time;

namespace PolicyApi.Tests;

/// <summary>
/// A SQLite in-memory database shared across the connection's lifetime (the brief's recommended
/// test strategy - a real relational provider, unlike the EF InMemory provider). Production runs on
/// SQL Server; the isolation mechanism under test (global query filters) is provider-agnostic, so
/// this faithfully exercises it. Each <see cref="Context"/> gets its own tenant identity, which is
/// exactly how cross-tenant access is proven.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = Context(tenantId: 0); // tenant value is irrelevant for schema creation
        ctx.Database.EnsureCreated();
    }

    /// <summary>A DbContext whose query filters are bound to <paramref name="tenantId"/>.</summary>
    public PolicyDbContext Context(int tenantId)
    {
        var options = new DbContextOptionsBuilder<PolicyDbContext>()
            .UseSqlite(_connection)
            .Options;

        var tenantContext = new TenantContext();
        tenantContext.Resolve(tenantId);
        return new PolicyDbContext(options, tenantContext);
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>A fixed clock so the expiring-window tests are deterministic.</summary>
public sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly UtcToday { get; } = today;
}
