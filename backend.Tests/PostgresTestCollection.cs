using backend.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace backend.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresTestCollection : ICollectionFixture<PostgresTestDatabase>
{
    public const string Name = "PostgreSQL integration tests";
}

public sealed class PostgresTestDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public MtsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MtsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        return new MtsDbContext(options);
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}
