using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AstronomyExplorer.Api.Tests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
  private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
      .Build();

  public string ConnectionString => _container.GetConnectionString();

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var context = CreateDbContext();
    await context.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  public AppDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(ConnectionString)
        .Options;

    return new AppDbContext(options);
  }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
  public const string Name = "PostgreSQL integration";
}
