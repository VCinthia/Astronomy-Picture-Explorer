using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Tests.Data;

[Collection(PostgreSqlCollection.Name)]
public sealed class LocalDevelopmentFixtureSeederTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task SeedAsync_IsIdempotentAndMarksTheFixtureCatalogReady()
  {
    var firstFrontendOrigin = new Uri("http://localhost:4310/");
    var secondFrontendOrigin = new Uri("http://localhost:4311/");
    await using var context = database.CreateDbContext();

    await context.CatalogSyncStates
      .Where(item => item.TargetFrom == LocalDevelopmentFixtureSeeder.FixtureDate &&
                     item.TargetTo == LocalDevelopmentFixtureSeeder.FixtureDate)
      .ExecuteDeleteAsync();
    await context.ApodEntries
      .Where(item => item.Date == LocalDevelopmentFixtureSeeder.FixtureDate)
      .ExecuteDeleteAsync();

    try
    {
      await LocalDevelopmentFixtureSeeder.SeedAsync(context, firstFrontendOrigin);
      context.ChangeTracker.Clear();
      await LocalDevelopmentFixtureSeeder.SeedAsync(context, secondFrontendOrigin);
      context.ChangeTracker.Clear();

      var entry = await context.ApodEntries.SingleAsync(
        item => item.Date == LocalDevelopmentFixtureSeeder.FixtureDate);
      var state = await context.CatalogSyncStates.SingleAsync(
        item => item.TargetFrom == LocalDevelopmentFixtureSeeder.FixtureDate &&
                item.TargetTo == LocalDevelopmentFixtureSeeder.FixtureDate);

      Assert.Equal("http://localhost:4311/local-apod/fixture.svg", entry.Url);
      Assert.Equal(CatalogSyncStatus.Completed, state.Status);
      Assert.Equal(LocalDevelopmentFixtureSeeder.FixtureDate, state.LastCompletedDate);
      Assert.Equal(1, state.SyncedEntryCount);
    }
    finally
    {
      await context.CatalogSyncStates
        .Where(item => item.TargetFrom == LocalDevelopmentFixtureSeeder.FixtureDate &&
                       item.TargetTo == LocalDevelopmentFixtureSeeder.FixtureDate)
        .ExecuteDeleteAsync();
      await context.ApodEntries
        .Where(item => item.Date == LocalDevelopmentFixtureSeeder.FixtureDate)
        .ExecuteDeleteAsync();
    }
  }
}
