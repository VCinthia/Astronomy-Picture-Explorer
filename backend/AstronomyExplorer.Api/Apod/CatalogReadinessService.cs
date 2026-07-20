using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Apod;

public sealed record CatalogReadinessSnapshot(
  DateOnly? TargetFrom,
  DateOnly? TargetTo,
  DateOnly? LastCompletedDate,
  string Status,
  bool Ready);

public sealed class CatalogReadinessService(
  AppDbContext dbContext,
  IOptions<CatalogOptions> catalogOptions)
{
  public async Task<CatalogReadinessSnapshot> GetAsync(
    CancellationToken cancellationToken)
  {
    var requiredFrom = catalogOptions.Value.RequiredFrom;
    var requiredTo = catalogOptions.Value.RequiredTo;
    var state = requiredFrom is null || requiredTo is null
      ? null
      : await dbContext.CatalogSyncStates
        .AsNoTracking()
        .SingleOrDefaultAsync(
          item => item.TargetFrom == requiredFrom && item.TargetTo == requiredTo,
          cancellationToken);
    var targetCount = state is null
      ? 0
      : await dbContext.ApodEntries.LongCountAsync(
        entry => entry.Date >= state.TargetFrom && entry.Date <= state.TargetTo,
        cancellationToken);
    var ready = state is not null &&
      state.Status == CatalogSyncStatus.Completed &&
      state.LastCompletedDate == state.TargetTo &&
      targetCount >= state.SyncedEntryCount;

    return new CatalogReadinessSnapshot(
      state?.TargetFrom ?? requiredFrom,
      state?.TargetTo ?? requiredTo,
      state?.LastCompletedDate,
      state?.Status.ToString().ToLowerInvariant() ?? "not_started",
      ready);
  }
}
