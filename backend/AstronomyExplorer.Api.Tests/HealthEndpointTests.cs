using System.Net;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HealthEndpointTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task GetHealth_PostgreSqlIsAvailable_ReturnsHealthy()
  {
    await using var factory = CreateFactory(database.ConnectionString, "Testing");
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var response = await client.GetAsync("/health", cancellation.Token);
    using var scope = factory.Services.CreateScope();
    var identityOptions = scope.ServiceProvider
      .GetRequiredService<IOptions<IdentityOptions>>()
      .Value;

    response.EnsureSuccessStatusCode();
    Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellation.Token));
    Assert.True(identityOptions.User.RequireUniqueEmail);
  }

  [Fact]
  public async Task GetHealth_PostgreSqlIsUnavailable_ReturnsServiceUnavailable()
  {
    const string unavailableConnection =
      "Host=127.0.0.1;Port=1;Database=unavailable;Username=postgres;Timeout=1;Command Timeout=1";
    await using var factory = CreateFactory(unavailableConnection, "Testing");
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var response = await client.GetAsync("/health", cancellation.Token);

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(cancellation.Token));
  }

  [Fact]
  public async Task OpenApi_IsMappedOnlyInDevelopment()
  {
    await using var developmentFactory = CreateFactory(database.ConnectionString, "Development");
    using var developmentClient = developmentFactory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var developmentResponse = await developmentClient.GetAsync(
      "/openapi/v1.json",
      cancellation.Token);

    Assert.Equal(HttpStatusCode.OK, developmentResponse.StatusCode);

    await using var productionFactory = CreateFactory(database.ConnectionString, "Production");
    using var productionClient = productionFactory.CreateClient();
    using var productionResponse = await productionClient.GetAsync(
      "/openapi/v1.json",
      cancellation.Token);

    Assert.Equal(HttpStatusCode.NotFound, productionResponse.StatusCode);
  }

  private static WebApplicationFactory<Program> CreateFactory(
    string connectionString,
    string environment)
  {
    return new WebApplicationFactory<Program>()
      .WithWebHostBuilder(builder => builder
        .UseEnvironment(environment)
        .UseSetting("ConnectionStrings:Postgres", connectionString)
        .UseSetting("Frontend:PublicBaseUrl", "https://portfolio.example")
        .UseSetting("Session:Issuer", "https://api.example.test")
        .UseSetting("Session:Audience", "astronomy-explorer-tests")
        .UseSetting("Session:SigningKey", "test-signing-key-at-least-32-bytes-long"));
  }
}
