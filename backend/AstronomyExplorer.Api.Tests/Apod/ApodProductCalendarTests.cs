using AstronomyExplorer.Api.Apod;

namespace AstronomyExplorer.Api.Tests.Apod;

public sealed class ApodProductCalendarTests
{
  [Theory]
  [InlineData(2026, 8, 13, 2, 59, 59, 12)]
  [InlineData(2026, 8, 13, 3, 0, 0, 13)]
  public void GetLatestAvailableDate_ArgentinaCalendarBoundary_ReturnsExpectedDate(
    int year,
    int month,
    int day,
    int hour,
    int minute,
    int second,
    int expectedDay)
  {
    var calendar = new ApodProductCalendar(new FixedTimeProvider(
      new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)));

    var actual = calendar.GetLatestAvailableDate();

    Assert.Equal(new DateOnly(year, month, expectedDay), actual);
  }

  private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => utcNow;
  }
}
