namespace AstronomyExplorer.Catalog;

public sealed record CatalogRuntimeSettings(
  string ConnectionString,
  string NasaApiKey);

public static class CatalogPreflight
{
  public static CatalogRuntimeSettings ValidateLive(
    CatalogSyncCommand command,
    Func<string, string?> readEnvironment)
  {
    if (IsRender(readEnvironment))
    {
      throw new CatalogSafetyException(
        "Catalog synchronization is prohibited on Render.");
    }

    var environment = FirstNonBlank(
      readEnvironment("DOTNET_ENVIRONMENT"),
      readEnvironment("ASPNETCORE_ENVIRONMENT")) ??
      "Development";
    if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase) &&
        !command.AllowLocalProduction)
    {
      throw new CatalogSafetyException(
        "Local Production execution requires --allow-local-production.");
    }

    var connectionString = readEnvironment("ConnectionStrings__Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new CatalogSafetyException("ConnectionStrings__Postgres is required.");
    }

    var nasaApiKey = readEnvironment("NasaApod__ApiKey");
    if (string.IsNullOrWhiteSpace(nasaApiKey) ||
        string.Equals(nasaApiKey.Trim(), "DEMO_KEY", StringComparison.OrdinalIgnoreCase))
    {
      throw new CatalogSafetyException(
        "A personal NASA API key is required; DEMO_KEY is not accepted.");
    }

    return new CatalogRuntimeSettings(connectionString.Trim(), nasaApiKey.Trim());
  }

  private static bool IsRender(Func<string, string?> readEnvironment) =>
    IsTruthy(readEnvironment("RENDER")) ||
    !string.IsNullOrWhiteSpace(readEnvironment("RENDER_SERVICE_ID")) ||
    !string.IsNullOrWhiteSpace(readEnvironment("RENDER_INSTANCE_ID"));

  private static bool IsTruthy(string? value) =>
    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

  private static string? FirstNonBlank(string? first, string? second) =>
    !string.IsNullOrWhiteSpace(first)
      ? first.Trim()
      : !string.IsNullOrWhiteSpace(second) ? second.Trim() : null;
}

public sealed class CatalogSafetyException(string message) : Exception(message);
