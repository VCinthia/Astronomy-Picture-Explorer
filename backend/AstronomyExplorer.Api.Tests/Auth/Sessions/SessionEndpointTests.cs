using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AstronomyExplorer.Api.Auth;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AstronomyExplorer.Api.Tests.Auth.Sessions;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionEndpointTests(PostgreSqlFixture database)
{
  private const string ValidPassword = "Valid1!Password";

  [Fact]
  public async Task Login_UnknownEmailOrWrongPassword_ReturnsSameInvalidCredentialsProblem()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("invalid-login");
    await CreateUserAsync(factory, email, emailConfirmed: true);

    using var unknown = await LoginAsync(client, UniqueEmail("unknown"), ValidPassword);
    using var wrong = await LoginAsync(client, email, "Wrong1!Password");
    var unknownProblem = await ReadProblemAsync(unknown);
    var wrongProblem = await ReadProblemAsync(wrong);

    Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    Assert.Equal(unknownProblem, wrongProblem);
    Assert.Contains("invalid_credentials", wrongProblem);
    AssertNoStore(unknown);
  }

  [Fact]
  public async Task Login_CorrectPasswordWithUnconfirmedEmail_ReturnsEmailUnconfirmed()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("unconfirmed-login");
    await CreateUserAsync(factory, email, emailConfirmed: false);

    using var response = await LoginAsync(client, email, ValidPassword);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Contains("email_unconfirmed", await ReadProblemAsync(response));
    AssertNoStore(response);
  }

  [Fact]
  public async Task Login_ConfirmedUser_ReturnsJwtAndExactHostOnlyRefreshCookie()
  {
    var now = DateTimeOffset.UtcNow;
    var timeProvider = new MutableTimeProvider(now);
    await using var factory = new SessionApiFactory(
      database.ConnectionString,
      timeProvider: timeProvider);
    using var client = CreateClient(factory);
    var email = UniqueEmail("successful-login");
    var user = await CreateUserAsync(factory, email, emailConfirmed: true);

    using var response = await LoginAsync(client, email, ValidPassword);
    var session = await ReadSessionAsync(response);
    var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(user.Id, session.User.Id);
    Assert.Equal(email, session.User.Email);
    Assert.Equal(now.AddMinutes(10), session.ExpiresAt);
    Assert.Contains($"{SessionApiFactory.CookieName}=", cookie);
    Assert.Contains("path=/auth", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    AssertNoStore(response);

    await using var context = database.CreateDbContext();
    var persisted = await context.RefreshSessions.SingleAsync(
      candidate => candidate.UserId == user.Id);
    var rawToken = ExtractCookieValue(cookie);
    Assert.Equal(64, persisted.TokenHash.Length);
    Assert.NotEqual(rawToken, persisted.TokenHash);
    Assert.Equal(RefreshSessionService.Hash(rawToken), persisted.TokenHash);
    Assert.DoesNotContain(rawToken, await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task RefreshCookie_DevelopmentHttp_DisablesSecureOnlyForLoopbackHost()
  {
    await using var factory = new SessionApiFactory(
      database.ConnectionString,
      environment: "Development");
    using var loopbackClient = CreateClient(factory);
    using var publicHostClient = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      BaseAddress = new Uri("http://public.example"),
      HandleCookies = false
    });
    var loopbackEmail = UniqueEmail("loopback-cookie");
    var publicHostEmail = UniqueEmail("public-cookie");
    await CreateUserAsync(factory, loopbackEmail, emailConfirmed: true);
    await CreateUserAsync(factory, publicHostEmail, emailConfirmed: true);

    using var loopbackLogin = await LoginAsync(loopbackClient, loopbackEmail, ValidPassword);
    using var publicHostLogin = await LoginAsync(publicHostClient, publicHostEmail, ValidPassword);
    var loopbackCookie = Assert.Single(loopbackLogin.Headers.GetValues("Set-Cookie"));
    var publicHostCookie = Assert.Single(publicHostLogin.Headers.GetValues("Set-Cookie"));

    Assert.DoesNotContain("secure", loopbackCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", publicHostCookie, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void JwtToken_ConfiguredClaimsSignatureAndExpiration_AreValidAndStrict()
  {
    var now = DateTimeOffset.UtcNow;
    var options = Options.Create(ValidOptions());
    var user = new ApplicationUser
    {
      Id = Guid.NewGuid(),
      Email = "jwt@example.test",
      UserName = "jwt@example.test"
    };
    var service = new JwtTokenService(options, new MutableTimeProvider(now));
    var result = service.Create(user);
    var handler = new JwtSecurityTokenHandler();
    var principal = handler.ValidateToken(
      result.Value,
      JwtTokenService.CreateValidationParameters(options.Value),
      out var validatedToken);
    var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);

    Assert.NotNull(principal.Identity);
    Assert.Equal(user.Id.ToString(), jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
    Assert.Equal(user.Email, jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
    Assert.False(string.IsNullOrWhiteSpace(
      jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value));
    Assert.False(string.IsNullOrWhiteSpace(
      jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Iat).Value));
    Assert.Equal(
      options.Value.ClientId,
      jwt.Claims.Single(claim => claim.Type == "client_id").Value);
    Assert.Equal(SessionApiFactory.Issuer, jwt.Issuer);
    Assert.Contains(SessionApiFactory.Audience, jwt.Audiences);
    Assert.Equal(now.AddMinutes(10), result.ExpiresAt);

    var expiredService = new JwtTokenService(
      options,
      new MutableTimeProvider(now.Subtract(TimeSpan.FromHours(1))));
    var expired = expiredService.Create(user);
    Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(
      expired.Value,
      JwtTokenService.CreateValidationParameters(options.Value),
      out _));

    var hs512Token = new JwtSecurityToken(
      options.Value.Issuer,
      options.Value.Audience,
      expires: now.AddMinutes(10).UtcDateTime,
      signingCredentials: new SigningCredentials(
        new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.Value.SigningKey)),
        SecurityAlgorithms.HmacSha512));
    Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(
      handler.WriteToken(hs512Token),
      JwtTokenService.CreateValidationParameters(options.Value),
      out _));
  }

  [Fact]
  public async Task Login_UnknownWrongOrEmptyPassword_PerformsOneHasherVerificationEach()
  {
    var passwordHasher = new CountingPasswordHasher();
    await using var factory = new SessionApiFactory(
      database.ConnectionString,
      passwordHasher: passwordHasher);
    using var client = CreateClient(factory);
    var email = UniqueEmail("counted-login");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    passwordHasher.Reset();

    using var unknown = await LoginAsync(client, UniqueEmail("counted-unknown"), ValidPassword);
    Assert.Equal(1, passwordHasher.VerificationCount);
    using var wrong = await LoginAsync(client, email, "Wrong1!Password");
    Assert.Equal(2, passwordHasher.VerificationCount);
    using var empty = await LoginAsync(client, email, string.Empty);
    Assert.Equal(3, passwordHasher.VerificationCount);

    Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, empty.StatusCode);
  }

  [Fact]
  public async Task Login_UserWithoutPasswordHash_ReturnsInvalidCredentialsForDummyPassword()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("passwordless");
    using (var scope = factory.Services.CreateScope())
    {
      var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      var user = new ApplicationUser
      {
        Email = email,
        UserName = email,
        EmailConfirmed = true
      };
      var result = await userManager.CreateAsync(user);
      Assert.True(result.Succeeded);
    }

    using var response = await LoginAsync(
      client,
      email,
      "Dummy1!Password-Not-A-Credential");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Contains("invalid_credentials", await ReadProblemAsync(response));
  }

  [Fact]
  public async Task Refresh_AllowedOrigin_RotatesCookieAndReturnsSameSessionContract()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("rotation");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var oldToken = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));

    using var refresh = await RefreshAsync(client, oldToken, SessionApiFactory.AllowedOrigin);
    var refreshedSession = await ReadSessionAsync(refresh);
    var newToken = ExtractCookieValue(Assert.Single(refresh.Headers.GetValues("Set-Cookie")));

    Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    Assert.Equal(email, refreshedSession.User.Email);
    Assert.NotEqual(oldToken, newToken);
    AssertNoStore(refresh);
    await using var context = database.CreateDbContext();
    var oldSession = await context.RefreshSessions.SingleAsync(
      candidate => candidate.TokenHash == RefreshSessionService.Hash(oldToken));
    var newSession = await context.RefreshSessions.SingleAsync(
      candidate => candidate.TokenHash == RefreshSessionService.Hash(newToken));
    Assert.NotNull(oldSession.RevokedAt);
    Assert.Equal(newSession.Id, oldSession.ReplacedByTokenId);
    Assert.Equal(oldSession.FamilyId, newSession.FamilyId);
  }

  [Fact]
  public async Task Refresh_MissingCookie_ReturnsGeneric401AndExpiresCookie()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
    request.Headers.Add("Origin", SessionApiFactory.AllowedOrigin);

    using var response = await client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Contains("invalid_refresh_token", await ReadProblemAsync(response));
    Assert.Contains(
      "max-age=0",
      Assert.Single(response.Headers.GetValues("Set-Cookie")),
      StringComparison.OrdinalIgnoreCase);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("https://attacker.example")]
  [InlineData("not-an-origin")]
  [InlineData("https://portfolio.example, https://attacker.example")]
  public async Task Refresh_InvalidOrigin_Returns403BeforeCookieMutation(string? origin)
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("origin");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var token = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));

    using var response = await RefreshAsync(client, token, origin);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Contains("invalid_origin", await ReadProblemAsync(response));
    Assert.False(response.Headers.Contains("Set-Cookie"));
    await using var context = database.CreateDbContext();
    Assert.Null((await context.RefreshSessions.SingleAsync(
      candidate => candidate.TokenHash == RefreshSessionService.Hash(token))).RevokedAt);
  }

  [Fact]
  public async Task Refresh_TwoConcurrentConsumers_OnlyOneSucceedsAndReplayRevokesFamily()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("concurrent");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var token = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));

    using var requestOne = CreateRefreshRequest(token, SessionApiFactory.AllowedOrigin);
    using var requestTwo = CreateRefreshRequest(token, SessionApiFactory.AllowedOrigin);
    var responses = await Task.WhenAll(
      client.SendAsync(requestOne),
      client.SendAsync(requestTwo));
    using var first = responses[0];
    using var second = responses[1];

    Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
    Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized));
    Assert.Contains(
      "invalid_refresh_token",
      await ReadProblemAsync(responses.Single(response => response.StatusCode == HttpStatusCode.Unauthorized)));
    await using var context = database.CreateDbContext();
    var familyId = await context.RefreshSessions
      .Where(candidate => candidate.TokenHash == RefreshSessionService.Hash(token))
      .Select(candidate => candidate.FamilyId)
      .SingleAsync();
    Assert.All(
      await context.RefreshSessions.Where(candidate => candidate.FamilyId == familyId).ToListAsync(),
      session => Assert.NotNull(session.RevokedAt));
  }

  [Fact]
  public async Task Refresh_ReplayedRotatedToken_RevokesReplacementFamily()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("replay");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var oldToken = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));
    using var rotation = await RefreshAsync(client, oldToken, SessionApiFactory.AllowedOrigin);
    var replacement = ExtractCookieValue(Assert.Single(rotation.Headers.GetValues("Set-Cookie")));

    using var replay = await RefreshAsync(client, oldToken, SessionApiFactory.AllowedOrigin);
    using var replacementAttempt = await RefreshAsync(
      client,
      replacement,
      SessionApiFactory.AllowedOrigin);

    Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, replacementAttempt.StatusCode);
    Assert.Contains("invalid_refresh_token", await ReadProblemAsync(replay));
  }

  [Fact]
  public async Task Refresh_ReplayOldTokenRacingActiveRotation_LeavesNoActiveFamilySession()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("replay-race");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var oldToken = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));
    using var firstRotation = await RefreshAsync(client, oldToken, SessionApiFactory.AllowedOrigin);
    var activeToken = ExtractCookieValue(Assert.Single(firstRotation.Headers.GetValues("Set-Cookie")));

    var responses = await Task.WhenAll(
      RefreshAsync(client, oldToken, SessionApiFactory.AllowedOrigin),
      RefreshAsync(client, activeToken, SessionApiFactory.AllowedOrigin));
    foreach (var response in responses)
    {
      response.Dispose();
    }

    await using var context = database.CreateDbContext();
    var familyId = await context.RefreshSessions
      .Where(session => session.TokenHash == RefreshSessionService.Hash(oldToken))
      .Select(session => session.FamilyId)
      .SingleAsync();
    Assert.False(await context.RefreshSessions.AnyAsync(
      session => session.FamilyId == familyId && session.RevokedAt == null));
  }

  [Fact]
  public async Task Logout_KnownFamilyTokenRacingRotation_RevokesFamilyOnly()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("logout-race");
    var user = await CreateUserAsync(factory, email, emailConfirmed: true);
    using var firstLogin = await LoginAsync(client, email, ValidPassword);
    var oldToken = ExtractCookieValue(Assert.Single(firstLogin.Headers.GetValues("Set-Cookie")));
    using var firstRotation = await RefreshAsync(client, oldToken, SessionApiFactory.AllowedOrigin);
    var activeToken = ExtractCookieValue(Assert.Single(firstRotation.Headers.GetValues("Set-Cookie")));
    using var otherFamilyLogin = await LoginAsync(client, email, ValidPassword);
    var otherFamilyToken = ExtractCookieValue(
      Assert.Single(otherFamilyLogin.Headers.GetValues("Set-Cookie")));

    var responses = await Task.WhenAll(
      LogoutAsync(client, oldToken, includeExpiredBearer: false),
      RefreshAsync(client, activeToken, SessionApiFactory.AllowedOrigin));
    foreach (var response in responses)
    {
      response.Dispose();
    }

    await using var context = database.CreateDbContext();
    var loggedOutFamilyId = await context.RefreshSessions
      .Where(session => session.TokenHash == RefreshSessionService.Hash(oldToken))
      .Select(session => session.FamilyId)
      .SingleAsync();
    Assert.False(await context.RefreshSessions.AnyAsync(
      session => session.FamilyId == loggedOutFamilyId && session.RevokedAt == null));
    var otherFamily = await context.RefreshSessions.SingleAsync(
      session => session.TokenHash == RefreshSessionService.Hash(otherFamilyToken));
    Assert.Equal(user.Id, otherFamily.UserId);
    Assert.Null(otherFamily.RevokedAt);
    Assert.NotEqual(loggedOutFamilyId, otherFamily.FamilyId);
  }

  [Fact]
  public async Task Refresh_ExpiredToken_ReturnsGeneric401AndExpiresCookie()
  {
    var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
    await using var factory = new SessionApiFactory(
      database.ConnectionString,
      timeProvider: timeProvider);
    using var client = CreateClient(factory);
    var email = UniqueEmail("expired-refresh");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var token = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));
    timeProvider.Advance(TimeSpan.FromDays(31));

    using var refresh = await RefreshAsync(client, token, SessionApiFactory.AllowedOrigin);

    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    Assert.Contains("invalid_refresh_token", await ReadProblemAsync(refresh));
    var deletion = Assert.Single(refresh.Headers.GetValues("Set-Cookie"));
    Assert.Contains("max-age=0", deletion, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Logout_ValidOriginAndCookie_RevokesAndIsIdempotentWithoutBearer()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("logout");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var token = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));

    using var first = await LogoutAsync(client, token, includeExpiredBearer: true);
    using var second = await LogoutAsync(client, token, includeExpiredBearer: false);

    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    Assert.Contains(
      "max-age=0",
      Assert.Single(first.Headers.GetValues("Set-Cookie")),
      StringComparison.OrdinalIgnoreCase);
    await using var context = database.CreateDbContext();
    Assert.NotNull((await context.RefreshSessions.SingleAsync(
      candidate => candidate.TokenHash == RefreshSessionService.Hash(token))).RevokedAt);
  }

  [Fact]
  public async Task Logout_InvalidOrigin_Returns403BeforeSessionOrCookieMutation()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);
    var email = UniqueEmail("logout-origin");
    await CreateUserAsync(factory, email, emailConfirmed: true);
    using var login = await LoginAsync(client, email, ValidPassword);
    var token = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));
    using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
    request.Headers.Add("Cookie", $"{SessionApiFactory.CookieName}={token}");
    request.Headers.Add("Origin", "https://attacker.example");

    using var response = await client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Contains("invalid_origin", await ReadProblemAsync(response));
    Assert.False(response.Headers.Contains("Set-Cookie"));
    await using var context = database.CreateDbContext();
    Assert.Null((await context.RefreshSessions.SingleAsync(
      candidate => candidate.TokenHash == RefreshSessionService.Hash(token))).RevokedAt);
  }

  [Fact]
  public async Task Login_IpLimitExceeded_ReturnsUniform429ProblemDetails()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AccountRateLimits:LoginIpPermitLimit"] = "2"
    };
    await using var factory = new SessionApiFactory(database.ConnectionString, settings);
    using var client = CreateClient(factory);

    using var first = await LoginAsync(client, UniqueEmail("limit-one"), ValidPassword);
    using var second = await LoginAsync(client, UniqueEmail("limit-two"), ValidPassword);
    using var limited = await LoginAsync(client, UniqueEmail("limit-three"), ValidPassword);

    Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    AssertNoStore(limited);
  }

  [Fact]
  public async Task Startup_InvalidSessionConfiguration_FailsClosed()
  {
    await using var factory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(builder => builder
        .UseEnvironment("Production")
        .UseSetting("ConnectionStrings:Postgres", database.ConnectionString)
        .UseSetting("Frontend:PublicBaseUrl", SessionApiFactory.AllowedOrigin)
        .UseSetting("Session:Issuer", SessionApiFactory.Issuer)
        .UseSetting("Session:Audience", SessionApiFactory.Audience)
        .UseSetting("Session:SigningKey", "too-short"));

    var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    Assert.Contains("at least 32", exception.Message);
  }

  [Fact]
  public async Task PublicHealthEndpoint_WithoutBearer_RemainsPublic()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = CreateClient(factory);

    using var response = await client.GetAsync("/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  private static HttpClient CreateClient(SessionApiFactory factory) => factory.CreateClient(
    new WebApplicationFactoryClientOptions { HandleCookies = false });

  private static async Task<ApplicationUser> CreateUserAsync(
    SessionApiFactory factory,
    string email,
    bool emailConfirmed)
  {
    using var scope = factory.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var user = new ApplicationUser
    {
      Email = email,
      UserName = email,
      EmailConfirmed = emailConfirmed
    };
    var result = await userManager.CreateAsync(user, ValidPassword);
    Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    return user;
  }

  private static Task<HttpResponseMessage> LoginAsync(
    HttpClient client,
    string email,
    string password) => client.PostAsJsonAsync("/auth/login", new { email, password });

  private static Task<HttpResponseMessage> RefreshAsync(
    HttpClient client,
    string token,
    string? origin) => client.SendAsync(CreateRefreshRequest(token, origin));

  private static HttpRequestMessage CreateRefreshRequest(string token, string? origin)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
    request.Headers.Add("Cookie", $"{SessionApiFactory.CookieName}={token}");
    if (origin is not null)
    {
      request.Headers.TryAddWithoutValidation("Origin", origin);
    }

    return request;
  }

  private static Task<HttpResponseMessage> LogoutAsync(
    HttpClient client,
    string token,
    bool includeExpiredBearer)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
    request.Headers.Add("Cookie", $"{SessionApiFactory.CookieName}={token}");
    request.Headers.Add("Origin", SessionApiFactory.AllowedOrigin);
    if (includeExpiredBearer)
    {
      request.Headers.Authorization = new("Bearer", "expired.access.token");
    }

    return client.SendAsync(request);
  }

  private static async Task<SessionPayload> ReadSessionAsync(HttpResponseMessage response)
  {
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<SessionPayload>())!;
  }

  private static async Task<string> ReadProblemAsync(HttpResponseMessage response)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var problem = document.RootElement;
    return string.Join(
      '|',
      problem.GetProperty("status").GetInt32(),
      problem.GetProperty("title").GetString(),
      problem.GetProperty("detail").GetString(),
      problem.GetProperty("type").GetString(),
      problem.TryGetProperty("code", out var code) ? code.GetString() : null);
  }

  private static string ExtractCookieValue(string setCookie)
  {
    var pair = setCookie.Split(';', 2)[0];
    return pair[(pair.IndexOf('=') + 1)..];
  }

  private static void AssertNoStore(HttpResponseMessage response) =>
    Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

  private static AuthSessionOptions ValidOptions() => new()
  {
    Issuer = SessionApiFactory.Issuer,
    Audience = SessionApiFactory.Audience,
    SigningKey = SessionApiFactory.SigningKey,
    AccessTokenLifetime = TimeSpan.FromMinutes(10),
    RefreshTokenLifetime = TimeSpan.FromDays(30),
    RefreshCookieName = SessionApiFactory.CookieName
  };

  private static string UniqueEmail(string prefix) =>
    $"{prefix}-{Guid.NewGuid():N}@example.test";

  private sealed record SessionPayload(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    SessionUserPayload User);

  private sealed record SessionUserPayload(Guid Id, string Email);
}
