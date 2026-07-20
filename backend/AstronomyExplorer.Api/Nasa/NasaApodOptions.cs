using System.Net;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Nasa;

public sealed class NasaApodOptions
{
  public const string SectionName = "NasaApod";

  public string ApiKey { get; init; } = string.Empty;

  public string BaseUrl { get; init; } = "https://api.nasa.gov/";

  public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);

  public int MaxAttempts { get; init; } = 2;
}

public sealed class NasaApodOptionsValidator(IHostEnvironment environment)
  : IValidateOptions<NasaApodOptions>
{
  public ValidateOptionsResult Validate(string? name, NasaApodOptions options)
  {
    var failures = new List<string>();
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
      failures.Add("NasaApod:ApiKey is required.");
    }

    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
        !string.IsNullOrEmpty(baseUri.UserInfo) ||
        baseUri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(baseUri.Query) ||
        !string.IsNullOrEmpty(baseUri.Fragment) ||
        (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp) ||
        (baseUri.Scheme == Uri.UriSchemeHttp && !IsApprovedDevelopmentHttpEndpoint(baseUri)))
    {
      failures.Add(
        "NasaApod:BaseUrl must be HTTPS, except for the approved local Development mock or loopback endpoint.");
    }

    if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(20))
    {
      failures.Add("NasaApod:Timeout must be between zero and 20 seconds.");
    }

    if (options.MaxAttempts is < 1 or > 2)
    {
      failures.Add("NasaApod:MaxAttempts must be 1 or 2.");
    }

    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }

  private bool IsApprovedDevelopmentHttpEndpoint(Uri baseUri) =>
    environment.IsDevelopment() &&
    (string.Equals(baseUri.Host, "nasa-mock", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
     IPAddress.TryParse(baseUri.Host, out var address) && IPAddress.IsLoopback(address));
}
