using AstronomyExplorer.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Data;

/// <summary>
/// Seeds the Compose-only catalog slice after migrations. It is intentionally opt-in
/// through LocalFixtures:Enabled and Program rejects that switch outside Development.
/// It never contacts NASA and is not a production catalog ingestion path.
/// </summary>
public static class LocalDevelopmentFixtureSeeder
{
  public static readonly DateOnly FixtureDate = new(2020, 1, 1);

  public static async Task SeedAsync(AppDbContext dbContext, Uri frontendBaseUri)
  {
    ArgumentNullException.ThrowIfNull(frontendBaseUri);

    var now = DateTimeOffset.UtcNow;
    var fixtureUrl = new Uri(frontendBaseUri, "local-apod/fixture.svg").AbsoluteUri;
    var entry = await dbContext.ApodEntries.SingleOrDefaultAsync(
      item => item.Date == FixtureDate);
    if (entry is null)
    {
      dbContext.ApodEntries.Add(new ApodEntry
      {
        Date = FixtureDate,
        Title = "Local astronomy fixture",
        Explanation = "A deterministic local APOD catalog fixture for search and favorites.",
        MediaType = "image",
        Url = fixtureUrl,
        CachedAt = now
      });
    }
    else
    {
      entry.Title = "Local astronomy fixture";
      entry.Explanation = "A deterministic local APOD catalog fixture for search and favorites.";
      entry.MediaType = "image";
      entry.Url = fixtureUrl;
      entry.HdUrl = null;
      entry.ThumbnailUrl = null;
      entry.Copyright = null;
      entry.CachedAt = now;
    }

    var syncState = await dbContext.CatalogSyncStates.SingleOrDefaultAsync(
      item => item.TargetFrom == FixtureDate && item.TargetTo == FixtureDate);
    if (syncState is null)
    {
      dbContext.CatalogSyncStates.Add(new CatalogSyncState
      {
        Id = Guid.NewGuid(),
        TargetFrom = FixtureDate,
        TargetTo = FixtureDate,
        LastCompletedDate = FixtureDate,
        SyncedEntryCount = 1,
        Status = CatalogSyncStatus.Completed,
        CreatedAt = now,
        UpdatedAt = now
      });
    }
    else
    {
      syncState.LastCompletedDate = FixtureDate;
      syncState.SyncedEntryCount = 1;
      syncState.Status = CatalogSyncStatus.Completed;
      syncState.LastError = null;
      syncState.RetryNotBefore = null;
      syncState.UpdatedAt = now;
    }

    await dbContext.SaveChangesAsync();
  }
}
