using AstronomyExplorer.Api.Email;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AstronomyExplorer.Api.Tests.Auth.Sessions;

public sealed class SessionApiFactory : WebApplicationFactory<Program>
{
  public const string Issuer = "https://api.example.test";
  public const string Audience = "astronomy-explorer-tests";
  public const string SigningKey =
    "test-signing-key-at-least-64-bytes-long-for-hs512-rejection-check";
  public const string NetlifyProxySigningKey =
    "test-netlify-proxy-signing-key-at-least-32-bytes-long";
  public const string AllowedOrigin = "https://portfolio.example";
  public const string CookieName = "ape.refresh";

  private readonly string _connectionString;
  private readonly IReadOnlyDictionary<string, string?> _settings;
  private readonly string _environment;
  private readonly TimeProvider _timeProvider;
  private readonly IPasswordHasher<ApplicationUser>? _passwordHasher;

  public SessionApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    string environment = "Production",
    TimeProvider? timeProvider = null,
    IPasswordHasher<ApplicationUser>? passwordHasher = null)
  {
    _connectionString = connectionString;
    _settings = settings ?? new Dictionary<string, string?>();
    _environment = environment;
    _timeProvider = timeProvider ?? TimeProvider.System;
    _passwordHasher = passwordHasher;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment(_environment);
    builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
    builder.UseSetting("Frontend:PublicBaseUrl", AllowedOrigin);
    builder.UseSetting("Session:Issuer", Issuer);
    builder.UseSetting("Session:Audience", Audience);
    builder.UseSetting("Session:SigningKey", SigningKey);
    builder.UseSetting("Session:AccessTokenLifetime", "00:10:00");
    builder.UseSetting("Session:RefreshTokenLifetime", "30.00:00:00");
    builder.UseSetting("Session:RefreshCookieName", CookieName);
    builder.UseSetting("NasaApod:ApiKey", "test-nasa-api-key");
    builder.UseSetting("NetlifyProxy:SigningKey", NetlifyProxySigningKey);

    foreach (var setting in _settings)
    {
      builder.UseSetting(setting.Key, setting.Value);
    }

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<TimeProvider>();
      services.AddSingleton(_timeProvider);
      if (_passwordHasher is not null)
      {
        services.RemoveAll<IPasswordHasher<ApplicationUser>>();
        services.AddSingleton(_passwordHasher);
      }
    });
  }

  public HttpClient CreateSignedClient(WebApplicationFactoryClientOptions? options = null)
  {
    var client = options is null
      ? base.CreateClient()
      : base.CreateClient(options);
    client.DefaultRequestHeaders.Add(
      NetlifyProxySignatureMiddleware.SignatureHeaderName,
      CreateNetlifySignature(expiresAt: _timeProvider.GetUtcNow().AddDays(365)));
    return client;
  }

  public static string CreateNetlifySignature(
    string? signingKey = null,
    string siteUrl = AllowedOrigin,
    string deployContext = "production",
    DateTimeOffset? expiresAt = null)
  {
    var header = WebEncoders.Base64UrlEncode(
      Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" })));
    var payload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
      iss = "netlify",
      site_url = siteUrl,
      deploy_context = deployContext,
      exp = (expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5)).ToUnixTimeSeconds()
    })));
    var signedValue = Encoding.ASCII.GetBytes($"{header}.{payload}");
    var signature = HMACSHA256.HashData(
      Encoding.UTF8.GetBytes(signingKey ?? NetlifyProxySigningKey),
      signedValue);
    return $"{header}.{payload}.{WebEncoders.Base64UrlEncode(signature)}";
  }
}

public sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
{
  private readonly PasswordHasher<ApplicationUser> _inner = new();
  private int _verificationCount;

  public int VerificationCount => Volatile.Read(ref _verificationCount);

  public string HashPassword(ApplicationUser user, string password) =>
    _inner.HashPassword(user, password);

  public PasswordVerificationResult VerifyHashedPassword(
    ApplicationUser user,
    string hashedPassword,
    string providedPassword)
  {
    Interlocked.Increment(ref _verificationCount);
    return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
  }

  public void Reset() => Interlocked.Exchange(ref _verificationCount, 0);
}

public sealed class MutableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
{
  private DateTimeOffset _utcNow = initialUtcNow;

  public override DateTimeOffset GetUtcNow() => _utcNow;

  public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}
