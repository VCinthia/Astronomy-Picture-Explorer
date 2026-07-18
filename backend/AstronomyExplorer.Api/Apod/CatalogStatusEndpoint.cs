using System.Text.Json.Serialization;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Apod;

public sealed record CatalogStatusDto(
  [property: JsonPropertyName("count")] long Count,
  [property: JsonPropertyName("coverage_from")] DateOnly? CoverageFrom,
  [property: JsonPropertyName("coverage_to")] DateOnly? CoverageTo,
  [property: JsonPropertyName("target_from")] DateOnly? TargetFrom,
  [property: JsonPropertyName("target_to")] DateOnly? TargetTo,
  [property: JsonPropertyName("last_completed_date")] DateOnly? LastCompletedDate,
  [property: JsonPropertyName("status")] string Status,
  [property: JsonPropertyName("ready")] bool Ready);

public static class CatalogStatusEndpoint
{
  public static IEndpointRouteBuilder MapCatalogStatusEndpoint(
    this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapGet("/api/apod/catalog-status", GetAsync)
      .WithTags("APOD")
      .WithName("GetApodCatalogStatus");
    return endpoints;
  }

  private static async Task<IResult> GetAsync(
    AppDbContext dbContext,
    IOptions<CatalogOptions> catalogOptions,
    CancellationToken cancellationToken)
  {
    var count = await dbContext.ApodEntries
      .AsNoTracking()
      .LongCountAsync(cancellationToken);
    var coverageFrom = count == 0
      ? null
      : await dbContext.ApodEntries.MinAsync(entry => (DateOnly?)entry.Date, cancellationToken);
    var coverageTo = count == 0
      ? null
      : await dbContext.ApodEntries.MaxAsync(entry => (DateOnly?)entry.Date, cancellationToken);
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

    return Results.Ok(new CatalogStatusDto(
      count,
      coverageFrom,
      coverageTo,
      state?.TargetFrom ?? requiredFrom,
      state?.TargetTo ?? requiredTo,
      state?.LastCompletedDate,
      state?.Status.ToString().ToLowerInvariant() ?? "not_started",
      ready));
  }
}
