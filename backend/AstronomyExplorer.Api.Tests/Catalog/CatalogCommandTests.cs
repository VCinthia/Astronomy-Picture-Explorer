using AstronomyExplorer.Catalog;

namespace AstronomyExplorer.Api.Tests.Catalog;

public sealed class CatalogCommandTests
{
  private static readonly DateOnly TodayUtc = new(2026, 7, 17);

  [Fact]
  public void NasaClientFactory_UsesBatchAppropriateTimeout()
  {
    using var client = CatalogNasaHttpClientFactory.Create();

    Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    Assert.Equal(new Uri("https://api.nasa.gov/"), client.BaseAddress);
  }

  [Fact]
  public async Task DryRun_PrintsEstimateWithoutReadingEnvironmentOrOpeningDependencies()
  {
    var output = new StringWriter();
    var error = new StringWriter();
    var environmentReads = 0;

    var exitCode = await CatalogProgram.RunAsync(
      [
        "catalog", "sync", "--from", "2026-01-01", "--to", "2026-01-31",
        "--dry-run"
      ],
      _ =>
      {
        environmentReads++;
        throw new InvalidOperationException("Dry-run must not inspect live configuration.");
      },
      output,
      error,
      TodayUtc,
      CancellationToken.None);

    Assert.Equal(0, exitCode);
    Assert.Equal(0, environmentReads);
    Assert.Contains("estimated NASA requests: 2", output.ToString(), StringComparison.Ordinal);
    Assert.Contains("No database or NASA request was opened", output.ToString(), StringComparison.Ordinal);
    Assert.Equal(string.Empty, error.ToString());
  }

  [Theory]
  [InlineData("1995-06-15", "1995-06-16", "30")]
  [InlineData("2026-07-18", "2026-07-18", "30")]
  [InlineData("2026-07-02", "2026-07-01", "30")]
  [InlineData("2026-07-01", "2026-07-02", "0")]
  [InlineData("2026-07-01", "2026-07-02", "31")]
  public void Parse_InvalidRangeOrBatch_RejectsCommand(string from, string to, string batch)
  {
    Assert.Throws<CatalogUsageException>(() => CatalogCommandParser.Parse(
      ["catalog", "sync", "--from", from, "--to", to, "--batch-size", batch],
      TodayUtc));
  }

  [Theory]
  [InlineData("--from")]
  [InlineData("--resume")]
  [InlineData("--dry-run")]
  public void Parse_DuplicateOptionOrFlag_IsRejected(string duplicate)
  {
    var args = new List<string>
    {
      "catalog", "sync", "--from", "2026-07-01", "--to", "2026-07-02"
    };
    if (duplicate == "--from")
    {
      args.AddRange(["--from", "2026-07-01"]);
    }
    else
    {
      args.AddRange([duplicate, duplicate]);
    }

    Assert.Throws<CatalogUsageException>(() =>
      CatalogCommandParser.Parse(args.ToArray(), TodayUtc));
  }

  [Theory]
  [InlineData("RENDER", "true")]
  [InlineData("RENDER_SERVICE_ID", "srv-test")]
  [InlineData("RENDER_INSTANCE_ID", "instance-test")]
  public void ValidateLive_RenderCannotBeOverridden(string marker, string value)
  {
    var command = CatalogCommandParser.Parse(
      [
        "catalog", "sync", "--from", "2026-07-01", "--to", "2026-07-02",
        "--allow-local-production"
      ],
      TodayUtc);
    var values = ValidEnvironment();
    values["DOTNET_ENVIRONMENT"] = "Production";
    values[marker] = value;

    var exception = Assert.Throws<CatalogSafetyException>(() =>
      CatalogPreflight.ValidateLive(command, key => values.GetValueOrDefault(key)));

    Assert.Contains("prohibited on Render", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void ValidateLive_LocalProductionRequiresExplicitOverride()
  {
    var command = CatalogCommandParser.Parse(
      ["catalog", "sync", "--from", "2026-07-01", "--to", "2026-07-02"],
      TodayUtc);
    var values = ValidEnvironment();
    values["DOTNET_ENVIRONMENT"] = "Production";

    Assert.Throws<CatalogSafetyException>(() =>
      CatalogPreflight.ValidateLive(command, key => values.GetValueOrDefault(key)));

    var allowed = command with { AllowLocalProduction = true };
    var settings = CatalogPreflight.ValidateLive(
      allowed,
      key => values.GetValueOrDefault(key));
    Assert.Equal("personal-key", settings.NasaApiKey);
  }

  [Fact]
  public void ValidateLive_EmptyDotnetEnvironmentFallsBackToAspnetProduction()
  {
    var command = CatalogCommandParser.Parse(
      ["catalog", "sync", "--from", "2026-07-01", "--to", "2026-07-02"],
      TodayUtc);
    var values = ValidEnvironment();
    values["DOTNET_ENVIRONMENT"] = "   ";
    values["ASPNETCORE_ENVIRONMENT"] = "Production";

    Assert.Throws<CatalogSafetyException>(() =>
      CatalogPreflight.ValidateLive(command, key => values.GetValueOrDefault(key)));
  }

  [Fact]
  public void ValidateLive_DemoKeyIsRejected()
  {
    var command = CatalogCommandParser.Parse(
      ["catalog", "sync", "--from", "2026-07-01", "--to", "2026-07-02"],
      TodayUtc);
    var values = ValidEnvironment();
    values["NasaApod__ApiKey"] = "DEMO_KEY";

    Assert.Throws<CatalogSafetyException>(() =>
      CatalogPreflight.ValidateLive(command, key => values.GetValueOrDefault(key)));
  }

  private static Dictionary<string, string?> ValidEnvironment() => new()
  {
    ["ConnectionStrings__Postgres"] = "Host=localhost;Database=test",
    ["NasaApod__ApiKey"] = "personal-key",
    ["DOTNET_ENVIRONMENT"] = "Development"
  };
}
