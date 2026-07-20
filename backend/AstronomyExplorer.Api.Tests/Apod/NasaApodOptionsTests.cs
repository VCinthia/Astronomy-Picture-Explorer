using AstronomyExplorer.Api.Nasa;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AstronomyExplorer.Api.Tests.Apod;

public sealed class NasaApodOptionsTests
{
  [Fact]
  public void Validate_ProductionHttpBaseUrl_Fails()
  {
    var result = new NasaApodOptionsValidator(Environment(Environments.Production))
      .Validate(null, Options("http://nasa-mock:8080/"));

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void Validate_DevelopmentLocalMockHttpBaseUrl_IsValid()
  {
    var result = new NasaApodOptionsValidator(Environment(Environments.Development))
      .Validate(null, Options("http://nasa-mock:8080/"));

    Assert.True(result.Succeeded);
  }

  [Fact]
  public void Validate_DevelopmentArbitraryHttpBaseUrl_Fails()
  {
    var result = new NasaApodOptionsValidator(Environment(Environments.Development))
      .Validate(null, Options("http://not-nasa.example/"));

    Assert.False(result.Succeeded);
  }

  private static NasaApodOptions Options(string baseUrl) => new()
  {
    ApiKey = "test-api-key",
    BaseUrl = baseUrl
  };

  private static IHostEnvironment Environment(string name) => new TestHostEnvironment
  {
    EnvironmentName = name
  };

  private sealed class TestHostEnvironment : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = "AstronomyExplorer.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
