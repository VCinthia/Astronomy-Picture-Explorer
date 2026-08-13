using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Nasa;
using Microsoft.AspNetCore.Authorization;

namespace AstronomyExplorer.Api.Favorites;

public static class FavoriteEndpoints
{
  public static IEndpointRouteBuilder MapFavoriteEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup("/api/favorites")
      .WithTags("Favorites")
      .RequireAuthorization();

    group.MapGet("", GetAllAsync)
      .WithName("GetFavorites");
    group.MapPost("", AddAsync)
      .WithName("AddFavorite");
    group.MapDelete("/{date}", RemoveAsync)
      .WithName("RemoveFavorite");

    return endpoints;
  }

  private static async Task<IResult> GetAllAsync(
    HttpContext httpContext,
    FavoriteService favoriteService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(httpContext, out var userId))
    {
      return FavoriteProblems.InvalidAuthenticatedUser();
    }

    return Results.Ok(await favoriteService.GetAllAsync(userId, cancellationToken));
  }

  private static async Task<IResult> AddAsync(
    CreateFavoriteRequest request,
    HttpContext httpContext,
    TimeProvider timeProvider,
    IApodProductCalendar calendar,
    ApodCacheService apodCacheService,
    FavoriteService favoriteService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(httpContext, out var userId))
    {
      return FavoriteProblems.InvalidAuthenticatedUser();
    }

    if (!TryGetValidApodDate(request.ApodDate, calendar, out var apodDate))
    {
      return FavoriteProblems.InvalidFavoriteDate();
    }

    try
    {
      await apodCacheService.GetAsync(apodDate, cancellationToken);
    }
    catch (NasaApodException exception)
    {
      return ApodProblems.Upstream(exception.Failure);
    }

    await favoriteService.AddAsync(
      userId,
      apodDate,
      timeProvider.GetUtcNow(),
      cancellationToken);
    return Results.NoContent();
  }

  private static async Task<IResult> RemoveAsync(
    string date,
    HttpContext httpContext,
    IApodProductCalendar calendar,
    FavoriteService favoriteService,
    CancellationToken cancellationToken)
  {
    if (!TryGetUserId(httpContext, out var userId))
    {
      return FavoriteProblems.InvalidAuthenticatedUser();
    }

    if (!DateOnly.TryParseExact(
          date,
          "yyyy-MM-dd",
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out var parsedDate) ||
        !TryGetValidApodDate(parsedDate, calendar, out var apodDate))
    {
      return FavoriteProblems.InvalidFavoriteDate();
    }

    await favoriteService.RemoveAsync(userId, apodDate, cancellationToken);
    return Results.NoContent();
  }

  private static bool TryGetUserId(HttpContext httpContext, out Guid userId) =>
    Guid.TryParse(
      httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
      out userId);

  private static bool TryGetValidApodDate(
    DateOnly? candidate,
    IApodProductCalendar calendar,
    out DateOnly apodDate)
  {
    apodDate = candidate.GetValueOrDefault();
    return candidate is not null &&
      apodDate >= ApodEndpoints.FirstApodDate &&
      apodDate <= calendar.GetLatestAvailableDate();
  }
}

public static class FavoriteProblems
{
  public static IResult InvalidFavoriteDate() => Create(
    StatusCodes.Status400BadRequest,
    "Invalid favorite APOD date.",
    "The APOD date must use YYYY-MM-DD and be within the supported APOD range.",
    "invalid_favorite_apod_date");

  public static IResult InvalidAuthenticatedUser() => Create(
    StatusCodes.Status401Unauthorized,
    "Invalid authenticated user.",
    "The authenticated session does not contain a valid user identifier.",
    "invalid_authenticated_user");

  private static IResult Create(int status, string title, string detail, string code) =>
    Results.Problem(
      statusCode: status,
      title: title,
      detail: detail,
      type: $"https://httpstatuses.com/{status}",
      extensions: new Dictionary<string, object?> { ["code"] = code });
}
