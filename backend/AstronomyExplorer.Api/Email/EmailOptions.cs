using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Email;

public sealed class FrontendOptions
{
  public const string SectionName = "Frontend";

  public string PublicBaseUrl { get; init; } = "http://localhost:4200";
}

public sealed class FrontendOptionsValidator(IHostEnvironment environment)
  : IValidateOptions<FrontendOptions>
{
  public ValidateOptionsResult Validate(string? name, FrontendOptions options)
  {
    if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        uri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
    {
      return ValidateOptionsResult.Fail(
        "Frontend:PublicBaseUrl must be an HTTP(S) origin without path, query or fragment.");
    }

    if (!environment.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps)
    {
      return ValidateOptionsResult.Fail(
        "Frontend:PublicBaseUrl must use HTTPS outside Development.");
    }

    return ValidateOptionsResult.Success;
  }
}

public sealed class ResendEmailOptions
{
  public const string SectionName = "Resend";

  public string ApiKey { get; init; } = string.Empty;

  public string FromAddress { get; init; } = string.Empty;
}
