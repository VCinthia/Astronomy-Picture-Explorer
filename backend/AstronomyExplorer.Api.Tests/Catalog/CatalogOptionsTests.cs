using AstronomyExplorer.Api.Apod;

namespace AstronomyExplorer.Api.Tests.Catalog;

public sealed class CatalogOptionsTests
{
  private static readonly TimeProvider Clock = new FixedTimeProvider(
    new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero));

  [Fact]
  public void Validate_UnconfiguredCanonicalRange_IsValid()
  {
    var result = new CatalogOptionsValidator(Clock).Validate(null, new CatalogOptions());

    Assert.True(result.Succeeded);
  }

  [Theory]
  [InlineData("1995-06-15", "1995-06-20")]
  [InlineData("2026-07-01", null)]
  [InlineData("2026-07-10", "2026-07-01")]
  [InlineData("2026-07-01", "2026-07-18")]
  public void Validate_InvalidCanonicalRange_Fails(string from, string? to)
  {
    var result = new CatalogOptionsValidator(Clock).Validate(null, new CatalogOptions
    {
      RequiredFrom = DateOnly.Parse(from),
      RequiredTo = to is null ? null : DateOnly.Parse(to)
    });

    Assert.True(result.Failed);
  }

  private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => utcNow;
  }
}
