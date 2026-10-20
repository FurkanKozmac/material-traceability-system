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
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16.10-alpine@sha256:029660641a0cfc575b14f336ba448fb8a75fd595d42e1fa316b9fb4378742297")
        .WithImage("postgres:16.10-alpine@sha256:029660641a0cfc575b14f336ba448fb8a75fd595d42e1fa316b9fb4378742297")
        .Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public string ConnectionString => container.GetConnectionString();

    public MtsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MtsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        return new MtsDbContext(options);
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}
