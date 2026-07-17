using System.Text;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Auth;

public sealed class AuthSessionOptions
{
  public const string SectionName = "Session";

  public string Issuer { get; init; } = string.Empty;

  public string Audience { get; init; } = string.Empty;

  public string SigningKey { get; init; } = string.Empty;

  public string ClientId { get; init; } = "astronomy-explorer-spa";

  public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(10);

  public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

  public string RefreshCookieName { get; init; } = "ape.refresh";
}

public sealed class SessionOptionsValidator : IValidateOptions<AuthSessionOptions>
{
  public ValidateOptionsResult Validate(string? name, AuthSessionOptions options)
  {
    var failures = new List<string>();
    if (string.IsNullOrWhiteSpace(options.Issuer))
    {
      failures.Add("Session:Issuer is required.");
    }

    if (string.IsNullOrWhiteSpace(options.Audience))
    {
      failures.Add("Session:Audience is required.");
    }

    if (Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) < 32)
    {
      failures.Add("Session:SigningKey must contain at least 32 UTF-8 bytes.");
    }

    if (string.IsNullOrWhiteSpace(options.ClientId))
    {
      failures.Add("Session:ClientId is required.");
    }

    if (options.AccessTokenLifetime <= TimeSpan.Zero)
    {
      failures.Add("Session:AccessTokenLifetime must be positive.");
    }

    if (options.RefreshTokenLifetime <= options.AccessTokenLifetime)
    {
      failures.Add("Session:RefreshTokenLifetime must exceed the access token lifetime.");
    }

    if (!IsValidCookieName(options.RefreshCookieName))
    {
      failures.Add("Session:RefreshCookieName is invalid.");
    }

    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }

  private static bool IsValidCookieName(string? value)
  {
    const string separators = "()<>@,;:\\\"/[]?={}";
    return !string.IsNullOrWhiteSpace(value) && value.All(character =>
      character > 0x20 && character < 0x7f && !separators.Contains(character));
  }
}
