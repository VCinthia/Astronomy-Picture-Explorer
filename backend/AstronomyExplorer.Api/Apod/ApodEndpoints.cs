using System.Globalization;
using AstronomyExplorer.Api.Nasa;

namespace AstronomyExplorer.Api.Apod;

public static class ApodEndpoints
{
  public static readonly DateOnly FirstApodDate = new(1995, 6, 16);
  public const int DefaultSearchPageSize = 12;
  public const int MaxSearchPage = 1_000;
  public const int MaxSearchPageSize = 30;
  public const int MaxSearchQueryLength = 200;

  public static IEndpointRouteBuilder MapApodEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup("/api/apod")
      .WithTags("APOD");

    group.MapGet("/today", GetTodayAsync)
      .WithName("GetTodayApod");
    group.MapGet("/date/{date}", GetByDateAsync)
      .WithName("GetApodByDate");
    group.MapGet("/search", SearchAsync)
      .WithName("SearchApod");

    return endpoints;
  }

  private static async Task<IResult> SearchAsync(
    string? q,
    ApodSearchService searchService,
    CatalogReadinessService readinessService,
    CancellationToken cancellationToken,
    int page = 1,
    int pageSize = DefaultSearchPageSize)
  {
    var query = q?.Trim();
    if (string.IsNullOrWhiteSpace(query) || query.Length > MaxSearchQueryLength)
    {
      return ApodProblems.InvalidSearchQuery();
    }

    if (page is < 1 or > MaxSearchPage || pageSize is < 1 or > MaxSearchPageSize)
    {
      return ApodProblems.InvalidSearchPagination();
    }

    var readiness = await readinessService.GetAsync(cancellationToken);
    if (!readiness.Ready)
    {
      return ApodProblems.CatalogNotReady();
    }

    var results = await searchService.SearchAsync(
      query,
      page,
      pageSize,
      cancellationToken);
    return Results.Ok(results);
  }

  private static Task<IResult> GetTodayAsync(
    ApodCacheService cacheService,
    TimeProvider timeProvider,
    CancellationToken cancellationToken)
  {
    var todayUtc = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    return GetValidatedAsync(todayUtc, todayUtc, cacheService, cancellationToken);
  }

  private static Task<IResult> GetByDateAsync(
    string date,
    ApodCacheService cacheService,
    TimeProvider timeProvider,
    CancellationToken cancellationToken)
  {
    var todayUtc = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    if (!DateOnly.TryParseExact(
          date,
          "yyyy-MM-dd",
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out var requestedDate))
    {
      return Task.FromResult(ApodProblems.InvalidDate());
    }

    return GetValidatedAsync(requestedDate, todayUtc, cacheService, cancellationToken);
  }

  private static async Task<IResult> GetValidatedAsync(
    DateOnly date,
    DateOnly todayUtc,
    ApodCacheService cacheService,
    CancellationToken cancellationToken)
  {
    if (date < FirstApodDate || date > todayUtc)
    {
      return ApodProblems.InvalidDate();
    }

    try
    {
      return Results.Ok(await cacheService.GetAsync(date, cancellationToken));
    }
    catch (NasaApodException exception)
    {
      return ApodProblems.Upstream(exception.Failure);
    }
  }
}

public static class ApodProblems
{
  public static IResult InvalidDate() => Create(
    StatusCodes.Status400BadRequest,
    "Invalid APOD date.",
    "The date must use YYYY-MM-DD and be within the supported APOD range.",
    "invalid_apod_date");

  public static IResult InvalidSearchQuery() => Create(
    StatusCodes.Status400BadRequest,
    "Invalid APOD search query.",
    $"The search query must contain between 1 and {ApodEndpoints.MaxSearchQueryLength} characters.",
    "invalid_search_query");

  public static IResult InvalidSearchPagination() => Create(
    StatusCodes.Status400BadRequest,
    "Invalid APOD search pagination.",
    $"Page must be between 1 and {ApodEndpoints.MaxSearchPage}, and pageSize must be " +
    $"between 1 and {ApodEndpoints.MaxSearchPageSize}.",
    "invalid_search_pagination");

  public static IResult CatalogNotReady() => Create(
    StatusCodes.Status503ServiceUnavailable,
    "APOD catalog is not ready.",
    "The historical astronomy catalog is still being prepared. Try again later.",
    "catalog_not_ready");

  public static IResult Upstream(NasaApodFailure failure) => failure switch
  {
    NasaApodFailure.Timeout => Create(
      StatusCodes.Status504GatewayTimeout,
      "APOD provider timed out.",
      "The astronomy service did not respond in time. Try again.",
      "apod_upstream_timeout"),
    NasaApodFailure.RateLimited => Create(
      StatusCodes.Status503ServiceUnavailable,
      "APOD provider is temporarily unavailable.",
      "The astronomy service is temporarily limited. Try again later.",
      "apod_upstream_unavailable"),
    NasaApodFailure.InvalidPayload => Create(
      StatusCodes.Status502BadGateway,
      "Invalid APOD provider response.",
      "The astronomy service returned an invalid response.",
      "apod_invalid_payload"),
    _ => Create(
      StatusCodes.Status502BadGateway,
      "APOD provider error.",
      "The astronomy service could not retrieve this entry.",
      "apod_upstream_error")
  };

  private static IResult Create(int status, string title, string detail, string code) =>
    Results.Problem(
      statusCode: status,
      title: title,
      detail: detail,
      type: $"https://httpstatuses.com/{status}",
      extensions: new Dictionary<string, object?> { ["code"] = code });
}
