namespace AstronomyExplorer.Api.Apod;

public interface IApodProductCalendar
{
  DateOnly GetLatestAvailableDate();
}

public sealed class ApodProductCalendar(TimeProvider timeProvider) : IApodProductCalendar
{
  public const string IanaTimeZoneId = "America/Argentina/Buenos_Aires";
  public const string WindowsTimeZoneId = "Argentina Standard Time";

  private readonly TimeZoneInfo _timeZone = ResolveTimeZone();

  public DateOnly GetLatestAvailableDate()
  {
    var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _timeZone);
    return DateOnly.FromDateTime(localNow.DateTime);
  }

  private static TimeZoneInfo ResolveTimeZone()
  {
    var failures = new List<Exception>();
    foreach (var id in new[] { IanaTimeZoneId, WindowsTimeZoneId })
    {
      try
      {
        return TimeZoneInfo.FindSystemTimeZoneById(id);
      }
      catch (TimeZoneNotFoundException exception)
      {
        failures.Add(exception);
      }
      catch (InvalidTimeZoneException exception)
      {
        failures.Add(exception);
      }
    }

    throw new InvalidOperationException(
      $"The APOD product calendar requires the {IanaTimeZoneId} time zone " +
      $"(or its Windows equivalent, {WindowsTimeZoneId}).",
      new AggregateException(failures));
  }
}
