namespace AstronomyExplorer.Api.Email;

public sealed class FrontendOptions
{
  public const string SectionName = "Frontend";

  public string PublicBaseUrl { get; init; } = "http://localhost:4200";
}

public sealed class ResendEmailOptions
{
  public const string SectionName = "Resend";

  public string ApiKey { get; init; } = string.Empty;

  public string FromAddress { get; init; } = string.Empty;
}
