using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Nasa;

public sealed class NasaApodOptions
{
  public const string SectionName = "NasaApod";

  public string ApiKey { get; init; } = string.Empty;

  public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);

  public int MaxAttempts { get; init; } = 2;
}

public sealed class NasaApodOptionsValidator : IValidateOptions<NasaApodOptions>
{
  public ValidateOptionsResult Validate(string? name, NasaApodOptions options)
  {
    var failures = new List<string>();
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
      failures.Add("NasaApod:ApiKey is required.");
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
}
