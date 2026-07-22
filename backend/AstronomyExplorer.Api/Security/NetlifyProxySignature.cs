using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AstronomyExplorer.Api.Email;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AstronomyExplorer.Api.Security;

/// <summary>
/// Production requests to application routes must arrive through Netlify's signed
/// proxy redirects. The signature prevents a caller from using the public Render URL
/// as an alternate browser/API entry point.
/// </summary>
public sealed class NetlifyProxyOptions
{
  public const string SectionName = "NetlifyProxy";

  public string SigningKey { get; init; } = string.Empty;

  /// <summary>
  /// Netlify redirect rate limits own the visitor-IP partition in production. The API
  /// deliberately does not interpret X-Forwarded-For because that header is not an
  /// authenticated source of client identity on the free Render ingress.
  /// </summary>
  public bool UseEdgeRateLimits { get; init; } = true;
}

public sealed class NetlifyProxyOptionsValidator(IHostEnvironment environment)
  : IValidateOptions<NetlifyProxyOptions>
{
  public ValidateOptionsResult Validate(string? name, NetlifyProxyOptions options)
  {
    if (!environment.IsProduction())
    {
      return ValidateOptionsResult.Success;
    }

    if (string.IsNullOrWhiteSpace(options.SigningKey) ||
        Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
    {
      return ValidateOptionsResult.Fail(
        "NetlifyProxy:SigningKey must contain at least 32 UTF-8 bytes in Production.");
    }

    if (!options.UseEdgeRateLimits)
    {
      return ValidateOptionsResult.Fail(
        "NetlifyProxy:UseEdgeRateLimits must be enabled in Production.");
    }

    return ValidateOptionsResult.Success;
  }
}

public sealed class NetlifyProxySignatureMiddleware(
  RequestDelegate next,
  IHostEnvironment environment,
  IOptions<NetlifyProxyOptions> options,
  IOptions<FrontendOptions> frontendOptions,
  TimeProvider timeProvider)
{
  public const string SignatureHeaderName = "x-nf-sign";

  private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(options.Value.SigningKey);
  private readonly string _expectedSiteUrl = new Uri(frontendOptions.Value.PublicBaseUrl)
    .GetLeftPart(UriPartial.Authority);

  public async Task InvokeAsync(HttpContext context)
  {
    if (!environment.IsProduction() || !IsApplicationRoute(context.Request.Path))
    {
      await next(context);
      return;
    }

    StringValues signatures = context.Request.Headers[SignatureHeaderName];
    if (signatures.Count != 1 || !IsValidSignature(signatures[0]))
    {
      context.Response.Headers.CacheControl = "no-store";
      context.Response.Headers.Pragma = "no-cache";
      await Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Invalid proxy request.",
        detail: "This request must use the public application origin.",
        type: "https://httpstatuses.com/403",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_proxy_request" })
        .ExecuteAsync(context);
      return;
    }

    await next(context);
  }

  private static bool IsApplicationRoute(PathString path) =>
    path.StartsWithSegments("/api", StringComparison.Ordinal) ||
    path.StartsWithSegments("/auth", StringComparison.Ordinal);

  private bool IsValidSignature(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Contains(','))
    {
      return false;
    }

    var parts = value.Split('.', StringSplitOptions.None);
    if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
    {
      return false;
    }

    try
    {
      var header = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[0]));
      var payload = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
      var signature = WebEncoders.Base64UrlDecode(parts[2]);
      using (header)
      using (payload)
      {
        if (!HasExactString(header.RootElement, "alg", "HS256") ||
            !HasExactString(payload.RootElement, "iss", "netlify") ||
            !HasExpectedSiteUrl(payload.RootElement) ||
            !HasExactString(payload.RootElement, "deploy_context", "production") ||
            !HasFutureExpiration(payload.RootElement))
        {
          return false;
        }
      }

      var signedValue = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
      var expected = HMACSHA256.HashData(_signingKey, signedValue);
      return CryptographicOperations.FixedTimeEquals(expected, signature);
    }
    catch (FormatException)
    {
      return false;
    }
    catch (ArgumentException)
    {
      return false;
    }
    catch (JsonException)
    {
      return false;
    }
  }

  private bool HasFutureExpiration(JsonElement payload)
  {
    return payload.TryGetProperty("exp", out var expiration) &&
      expiration.ValueKind == JsonValueKind.Number &&
      expiration.TryGetInt64(out var unixSeconds) &&
      unixSeconds > timeProvider.GetUtcNow().ToUnixTimeSeconds();
  }

  private bool HasExpectedSiteUrl(JsonElement payload)
  {
    return payload.TryGetProperty("site_url", out var siteUrl) &&
      siteUrl.ValueKind == JsonValueKind.String &&
      string.Equals(
        siteUrl.GetString(),
        _expectedSiteUrl,
        StringComparison.OrdinalIgnoreCase);
  }

  private static bool HasExactString(JsonElement document, string propertyName, string expected) =>
    document.TryGetProperty(propertyName, out var property) &&
    property.ValueKind == JsonValueKind.String &&
    string.Equals(property.GetString(), expected, StringComparison.Ordinal);
}

public static class NetlifyProxySignatureApplicationBuilderExtensions
{
  public static IApplicationBuilder UseNetlifyProxySignature(this IApplicationBuilder app) =>
    app.UseMiddleware<NetlifyProxySignatureMiddleware>();
}
