using System.Text.Json.Serialization;
using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore;

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
    CatalogReadinessService readinessService,
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
    var readiness = await readinessService.GetAsync(cancellationToken);

    return Results.Ok(new CatalogStatusDto(
      count,
      coverageFrom,
      coverageTo,
      readiness.TargetFrom,
      readiness.TargetTo,
      readiness.LastCompletedDate,
      readiness.Status,
      readiness.Ready));
  }
}
