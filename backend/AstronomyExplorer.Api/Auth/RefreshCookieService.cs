using System.Net;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Auth;

public sealed class RefreshCookieService(
  IOptions<AuthSessionOptions> options,
  IWebHostEnvironment environment,
  TimeProvider timeProvider)
{
  private readonly AuthSessionOptions _options = options.Value;

  public string? Read(HttpRequest request) =>
    request.Cookies.TryGetValue(_options.RefreshCookieName, out var token)
      ? token
      : null;

  public void Write(HttpContext httpContext, string rawToken, DateTimeOffset expiresAt)
  {
    httpContext.Response.Cookies.Append(
      _options.RefreshCookieName,
      rawToken,
      CreateOptions(httpContext.Request, expiresAt, expiresAt - timeProvider.GetUtcNow()));
  }

  public void Delete(HttpContext httpContext)
  {
    var options = CreateOptions(httpContext.Request, DateTimeOffset.UnixEpoch, TimeSpan.Zero);
    httpContext.Response.Cookies.Append(_options.RefreshCookieName, string.Empty, options);
  }

  private CookieOptions CreateOptions(
    HttpRequest request,
    DateTimeOffset expires,
    TimeSpan maxAge) => new()
    {
      Domain = null,
      HttpOnly = true,
      Secure = !CanUseInsecureLocalDevelopment(request),
      SameSite = SameSiteMode.Lax,
      Path = "/auth",
      Expires = expires,
      MaxAge = maxAge,
      IsEssential = true
    };

  private bool CanUseInsecureLocalDevelopment(HttpRequest request) =>
    environment.IsDevelopment() &&
    !request.IsHttps &&
    IsLoopbackHost(request.Host.Host);

  private static bool IsLoopbackHost(string host) =>
    string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
    (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}
