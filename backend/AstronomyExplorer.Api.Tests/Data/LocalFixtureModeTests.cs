using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace AstronomyExplorer.Api.Tests.Data;

public sealed class LocalFixtureModeTests
{
  [Fact]
  public void Production_LocalFixturesEnabled_FailsBeforeTheHostStarts()
  {
    using var factory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(builder => builder
        .UseEnvironment(Environments.Production)
        .UseSetting("LocalFixtures:Enabled", "true"));

    var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

    Assert.Contains("LocalFixtures:Enabled is available only in Development.", exception.Message);
  }
}
