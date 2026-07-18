using System.Net;
using System.Text.Json;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Tests.Catalog;

[Collection(PostgreSqlCollection.Name)]
public sealed class CatalogStatusEndpointTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task GetStatus_WithoutTarget_ReturnsNotStartedAndNotReady()
  {
    await using (var context = database.CreateDbContext())
    {
      await context.CatalogSyncStates.ExecuteDeleteAsync();
    }

    using var factory = CreateFactory();
    using var client = factory.CreateClient();
    using var response = await client.GetAsync("/api/apod/catalog-status");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("not_started", document.RootElement.GetProperty("status").GetString());
    Assert.False(document.RootElement.GetProperty("ready").GetBoolean());
    Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("target_from").ValueKind);
  }

  [Fact]
  public async Task GetStatus_ConfiguredCompletedTargetWithSyncedCoverage_IsReady()
  {
    var from = new DateOnly(2013, 1, 1);
    var to = new DateOnly(2013, 1, 5);
    await SeedLatestCompletedTargetAsync(from, to, [from, to]);

    using var factory = CreateFactory(from, to);
    using var client = factory.CreateClient();
    using var response = await client.GetAsync("/api/apod/catalog-status");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
    Assert.Equal("2013-01-01", document.RootElement.GetProperty("target_from").GetString());
    Assert.Equal("2013-01-05", document.RootElement.GetProperty("last_completed_date").GetString());
    Assert.True(document.RootElement.GetProperty("ready").GetBoolean());
  }

  [Fact]
  public async Task GetStatus_CompletedCheckpointWithCoverageDrift_IsNotReady()
  {
    var from = new DateOnly(2013, 2, 1);
    var to = new DateOnly(2013, 2, 3);
    await SeedLatestCompletedTargetAsync(from, to);
    await using (var context = database.CreateDbContext())
    {
      await context.ApodEntries
        .Where(entry => entry.Date == new DateOnly(2013, 2, 2))
        .ExecuteDeleteAsync();
    }

    using var factory = CreateFactory(from, to);
    using var client = factory.CreateClient();
    using var response = await client.GetAsync("/api/apod/catalog-status");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.False(document.RootElement.GetProperty("ready").GetBoolean());
  }

  [Fact]
  public async Task GetStatus_NewerSmallSyncCannotReplaceConfiguredTarget()
  {
    var requiredFrom = new DateOnly(2013, 3, 1);
    var requiredTo = new DateOnly(2013, 3, 5);
    await SeedLatestCompletedTargetAsync(
      new DateOnly(2013, 4, 1),
      new DateOnly(2013, 4, 1));

    using var factory = CreateFactory(requiredFrom, requiredTo);
    using var client = factory.CreateClient();
    using var response = await client.GetAsync("/api/apod/catalog-status");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal("not_started", document.RootElement.GetProperty("status").GetString());
    Assert.False(document.RootElement.GetProperty("ready").GetBoolean());
    Assert.Equal("2013-03-01", document.RootElement.GetProperty("target_from").GetString());
    Assert.Equal("2013-03-05", document.RootElement.GetProperty("target_to").GetString());
  }

  private WebApplicationFactory<Program> CreateFactory(
    DateOnly? requiredFrom = null,
    DateOnly? requiredTo = null) => new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
      builder
      .UseEnvironment("Testing")
      .UseSetting("ConnectionStrings:Postgres", database.ConnectionString)
      .UseSetting("Frontend:PublicBaseUrl", "https://portfolio.example")
      .UseSetting("Session:Issuer", "https://api.example.test")
      .UseSetting("Session:Audience", "astronomy-explorer-tests")
      .UseSetting("Session:SigningKey", "test-signing-key-at-least-32-bytes-long")
      .UseSetting("NasaApod:ApiKey", "test-nasa-api-key");
      if (requiredFrom is not null && requiredTo is not null)
      {
        builder.UseSetting("Catalog:RequiredFrom", $"{requiredFrom:yyyy-MM-dd}");
        builder.UseSetting("Catalog:RequiredTo", $"{requiredTo:yyyy-MM-dd}");
      }
    });

  private async Task SeedLatestCompletedTargetAsync(
    DateOnly from,
    DateOnly to,
    IReadOnlyList<DateOnly>? returnedDates = null)
  {
    await using var context = database.CreateDbContext();
    await context.CatalogSyncStates.ExecuteDeleteAsync();
    await context.ApodEntries
      .Where(entry => entry.Date >= from && entry.Date <= to)
      .ExecuteDeleteAsync();
    var now = new DateTimeOffset(2026, 7, 17, 20, 0, 0, TimeSpan.Zero);
    var dates = returnedDates ?? Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
      .Select(from.AddDays)
      .ToArray();
    foreach (var date in dates)
    {
      context.ApodEntries.Add(new ApodEntry
      {
        Date = date,
        Title = $"APOD {date:yyyy-MM-dd}",
        Explanation = "Catalog status coverage.",
        MediaType = "image",
        Url = $"https://images.example/{date:yyyy-MM-dd}.jpg",
        CachedAt = now
      });
    }

    context.CatalogSyncStates.Add(new CatalogSyncState
    {
      Id = Guid.NewGuid(),
      TargetFrom = from,
      TargetTo = to,
      LastCompletedDate = to,
      SyncedEntryCount = dates.Count,
      Status = CatalogSyncStatus.Completed,
      CreatedAt = now,
      UpdatedAt = now
    });
    await context.SaveChangesAsync();
  }
}
