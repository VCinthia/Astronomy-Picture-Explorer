using AstronomyExplorer.Api.Email;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AstronomyExplorer.Api.Security;

public sealed class AllowedOriginFilter(
  IWebHostEnvironment environment,
  IOptions<FrontendOptions> options) : IEndpointFilter
{
  private readonly string _allowedOrigin = new Uri(options.Value.PublicBaseUrl).GetLeftPart(UriPartial.Authority);

  public async ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext context,
    EndpointFilterDelegate next)
  {
    context.HttpContext.Response.Headers.CacheControl = "no-store";
    context.HttpContext.Response.Headers.Pragma = "no-cache";
    if (!environment.IsProduction())
    {
      return await next(context);
    }

    StringValues originValues = context.HttpContext.Request.Headers.Origin;
    if (originValues.Count != 1 || !IsExactAllowedOrigin(originValues[0]))
    {
      return SessionProblems.InvalidOrigin();
    }

    return await next(context);
  }

  private bool IsExactAllowedOrigin(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Contains(','))
    {
      return false;
    }

    return Uri.TryCreate(value, UriKind.Absolute, out var origin) &&
      origin.GetLeftPart(UriPartial.Authority) == value.TrimEnd('/') &&
      string.Equals(
        origin.GetLeftPart(UriPartial.Authority),
        _allowedOrigin,
        StringComparison.OrdinalIgnoreCase);
  }
}

public static class SessionProblems
{
  public static IResult InvalidCredentials() => Create(
    StatusCodes.Status401Unauthorized,
    "Invalid credentials.",
    "The email or password is invalid.",
    "invalid_credentials");

  public static IResult EmailUnconfirmed() => Create(
    StatusCodes.Status403Forbidden,
    "Email confirmation required.",
    "Confirm your email before signing in.",
    "email_unconfirmed");

  public static IResult InvalidRefreshToken() => Create(
    StatusCodes.Status401Unauthorized,
    "Invalid refresh token.",
    "The session cannot be refreshed.",
    "invalid_refresh_token");

  public static IResult InvalidOrigin() => Create(
    StatusCodes.Status403Forbidden,
    "Invalid request origin.",
    "The request origin is not allowed.",
    "invalid_origin");

  private static IResult Create(int status, string title, string detail, string code) =>
    Results.Problem(
      statusCode: status,
      title: title,
      detail: detail,
      type: $"https://httpstatuses.com/{status}",
      extensions: new Dictionary<string, object?> { ["code"] = code });
}
