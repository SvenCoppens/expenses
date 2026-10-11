using Expenses.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Tests.Fixtures;

public sealed class SqliteTestDbFixtureTests : IClassFixture<SqliteTestDbFixture>
{
    private readonly SqliteTestDbFixture _fixture;

    public SqliteTestDbFixtureTests(SqliteTestDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateDbContextAsync_WhenCalled_ReturnsUsableContextAndCanConnect()
    {
        // Arrange & Act
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        context.Should().NotBeNull();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public void CreateContext_WhenCalled_ReturnsUsableContextInstance()
    {
        // Arrange & Act
        using var context = _fixture.CreateContext();
        var canConnect = context.Database.CanConnect();

        // Assert
        context.Should().NotBeNull();
        canConnect.Should().BeTrue();
    }
}
