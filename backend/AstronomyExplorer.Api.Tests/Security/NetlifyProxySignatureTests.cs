using System.Net;
using AstronomyExplorer.Api.Tests.Auth.Sessions;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Tests.Security;

[Collection(PostgreSqlCollection.Name)]
public sealed class NetlifyProxySignatureTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task ProductionApplicationRoute_WithoutSignature_RejectsDirectAndSpoofedRequest()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.99");

    using var response = await client.GetAsync("/api/apod/catalog-status");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Contains("invalid_proxy_request", await response.Content.ReadAsStringAsync());
    Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
  }

  [Fact]
  public async Task ProductionApplicationRoute_ValidNetlifySignature_ReachesEndpoint()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = factory.CreateSignedClient();

    using var response = await client.GetAsync("/api/apod/catalog-status");

    Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Theory]
  [InlineData("https://attacker.example", "production")]
  [InlineData(SessionApiFactory.AllowedOrigin, "deploy-preview")]
  public async Task ProductionApplicationRoute_WrongSignedClaims_AreRejected(
    string siteUrl,
    string deployContext)
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      HandleCookies = false
    });
    client.DefaultRequestHeaders.Add(
      "x-nf-sign",
      SessionApiFactory.CreateNetlifySignature(siteUrl: siteUrl, deployContext: deployContext));

    using var response = await client.GetAsync("/auth/register");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Contains("invalid_proxy_request", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task ProductionApplicationRoute_ExpiredOrWrongKeySignature_IsRejected()
  {
    var signatures = new[]
    {
      SessionApiFactory.CreateNetlifySignature(expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1)),
      SessionApiFactory.CreateNetlifySignature(signingKey: "another-test-signing-key-at-least-32-bytes-long")
    };

    foreach (var signature in signatures)
    {
      await using var factory = new SessionApiFactory(database.ConnectionString);
      using var client = factory.CreateClient();
      client.DefaultRequestHeaders.Add("x-nf-sign", signature);

      using var response = await client.GetAsync("/api/apod/catalog-status");

      Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
  }

  [Fact]
  public async Task Health_WithoutSignature_RemainsAvailableForRenderProbe()
  {
    await using var factory = new SessionApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Theory]
  [InlineData("NetlifyProxy:SigningKey", "")]
  [InlineData("NetlifyProxy:UseEdgeRateLimits", "false")]
  public void ProductionStartup_InvalidProxyConfiguration_FailsClosed(
    string key,
    string value)
  {
    var settings = new Dictionary<string, string?> { [key] = value };
    using var factory = new SessionApiFactory(database.ConnectionString, settings);

    var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

    Assert.Contains("NetlifyProxy", exception.Message);
  }
}
