using Expenses.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Tests.Fixtures;

public sealed class SqliteTestDbFixture : IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection _connection;
    public IDbContextFactory<ApplicationDbContext> Factory { get; }
    public DbContextOptions<ApplicationDbContext> Options { get; }

    public SqliteTestDbFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Factory = new TestDbContextFactory(Options);

        using var context = new ApplicationDbContext(Options);
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext() => Factory.CreateDbContext();

    public Task<ApplicationDbContext> CreateContextAsync(CancellationToken cancellationToken = default) =>
        Factory.CreateDbContextAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

internal sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext() => new(options);

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApplicationDbContext(options));
}
