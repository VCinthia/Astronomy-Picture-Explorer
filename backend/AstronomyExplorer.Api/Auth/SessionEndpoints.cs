using AstronomyExplorer.Api.Auth.Dtos;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Security;
using Microsoft.AspNetCore.Identity;

namespace AstronomyExplorer.Api.Auth;

public static class SessionEndpoints
{
  public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup("/auth")
      .WithTags("Sessions");

    group.MapPost("/login", LoginAsync)
      .WithName("LoginSession")
      .RequireRateLimiting(AccountRateLimitPolicies.LoginByIp);

    group.MapPost("/refresh", RefreshAsync)
      .WithName("RefreshSession")
      .AddEndpointFilter<AllowedOriginFilter>();

    group.MapPost("/logout", LogoutAsync)
      .WithName("LogoutSession")
      .AddEndpointFilter<AllowedOriginFilter>();

    return endpoints;
  }

  private static async Task<IResult> LoginAsync(
    LoginRequest request,
    UserManager<ApplicationUser> userManager,
    LoginPasswordVerifier passwordVerifier,
    RefreshSessionService refreshSessionService,
    RefreshCookieService refreshCookieService,
    JwtTokenService jwtTokenService,
    HttpContext httpContext,
    CancellationToken cancellationToken)
  {
    SetNoStore(httpContext.Response);
    var email = request.Email?.Trim();
    var user = string.IsNullOrWhiteSpace(email)
      ? null
      : await userManager.FindByEmailAsync(email);
    if (!passwordVerifier.Verify(user, request.Password))
    {
      return SessionProblems.InvalidCredentials();
    }

    var authenticatedUser = user!;
    if (!await userManager.IsEmailConfirmedAsync(authenticatedUser))
    {
      return SessionProblems.EmailUnconfirmed();
    }

    var refreshSession = await refreshSessionService.CreateAsync(authenticatedUser, cancellationToken);
    if (refreshSession is null)
    {
      return SessionProblems.InvalidCredentials();
    }

    refreshCookieService.Write(
      httpContext,
      refreshSession.RawToken,
      refreshSession.ExpiresAt);
    return Results.Ok(CreateResponse(authenticatedUser, jwtTokenService));
  }

  private static async Task<IResult> RefreshAsync(
    RefreshSessionService refreshSessionService,
    RefreshCookieService refreshCookieService,
    JwtTokenService jwtTokenService,
    HttpContext httpContext,
    CancellationToken cancellationToken)
  {
    SetNoStore(httpContext.Response);
    var rawToken = refreshCookieService.Read(httpContext.Request);
    if (string.IsNullOrWhiteSpace(rawToken))
    {
      refreshCookieService.Delete(httpContext);
      return SessionProblems.InvalidRefreshToken();
    }

    var rotation = await refreshSessionService.RotateAsync(rawToken, cancellationToken);
    if (!rotation.Succeeded || rotation.User is null || rotation.RawToken is null)
    {
      refreshCookieService.Delete(httpContext);
      return SessionProblems.InvalidRefreshToken();
    }

    refreshCookieService.Write(httpContext, rotation.RawToken, rotation.ExpiresAt);
    return Results.Ok(CreateResponse(rotation.User, jwtTokenService));
  }

  private static async Task<IResult> LogoutAsync(
    RefreshSessionService refreshSessionService,
    RefreshCookieService refreshCookieService,
    HttpContext httpContext,
    CancellationToken cancellationToken)
  {
    SetNoStore(httpContext.Response);
    var rawToken = refreshCookieService.Read(httpContext.Request);
    if (!string.IsNullOrWhiteSpace(rawToken))
    {
      await refreshSessionService.RevokeAsync(rawToken, cancellationToken);
    }

    refreshCookieService.Delete(httpContext);
    return Results.NoContent();
  }

  private static SessionResponse CreateResponse(
    ApplicationUser user,
    JwtTokenService jwtTokenService)
  {
    var accessToken = jwtTokenService.Create(user);
    return new SessionResponse(
      accessToken.Value,
      accessToken.ExpiresAt,
      new SessionUserResponse(user.Id, user.Email!));
  }

  private static void SetNoStore(HttpResponse response)
  {
    response.Headers.CacheControl = "no-store";
    response.Headers.Pragma = "no-cache";
  }
}
