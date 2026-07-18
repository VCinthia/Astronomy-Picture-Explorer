using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AstronomyExplorer.Catalog;

namespace AstronomyExplorer.Api.Tests.Catalog;

public sealed class NasaCatalogClientTests
{
  [Fact]
  public async Task FetchRange_UsesExactQueryAndReturnsSparseEntriesSorted()
  {
    var handler = new StubHandler(_ => JsonResponse(Payload("2026-07-02", "2026-07-01")));
    var delay = new FakeDelay();
    var client = CreateClient(handler, delay, "private-key");

    var entries = await client.FetchRangeAsync(
      new DateOnly(2026, 7, 1),
      new DateOnly(2026, 7, 2),
      CancellationToken.None);

    Assert.Equal(2, entries.Count);
    Assert.Equal(new DateOnly(2026, 7, 1), entries[0].Date);
    Assert.Null(entries[0].HdUrl);
    Assert.Null(entries[0].ThumbnailUrl);
    Assert.Null(entries[0].Copyright);
    Assert.Equal("private-key", handler.ApiKey);
    Assert.Equal(
      "?start_date=2026-07-01&end_date=2026-07-02&thumbs=true",
      handler.RequestUri?.Query);
    Assert.DoesNotContain("api_key", handler.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    Assert.Empty(delay.Delays);
  }

  [Fact]
  public async Task FetchRange_HistoricalCalendarGapsAndEmptyArrayAreValid()
  {
    var sparseHandler = new StubHandler(_ => JsonResponse(
      Payload("1995-06-20", "1995-06-16")));
    var sparse = await CreateClient(sparseHandler, new FakeDelay()).FetchRangeAsync(
      new DateOnly(1995, 6, 16),
      new DateOnly(1995, 6, 20),
      CancellationToken.None);

    Assert.Equal(
      [new DateOnly(1995, 6, 16), new DateOnly(1995, 6, 20)],
      sparse.Select(entry => entry.Date).ToArray());

    var emptyHandler = new StubHandler(_ => JsonResponse("[]"));
    var empty = await CreateClient(emptyHandler, new FakeDelay()).FetchRangeAsync(
      new DateOnly(1995, 6, 17),
      new DateOnly(1995, 6, 19),
      CancellationToken.None);
    Assert.Empty(empty);
  }

  [Theory]
  [InlineData("null-item")]
  [InlineData("duplicate")]
  [InlineData("out-of-range")]
  [InlineData("invalid-field")]
  public async Task FetchRange_InvalidBatchIsRejectedBeforePersistence(string scenario)
  {
    var json = scenario switch
    {
      "null-item" => "[null]",
      "duplicate" => Payload("2026-07-01", "2026-07-01"),
      "out-of-range" => Payload("2026-07-01", "2026-07-03"),
      _ => Payload("2026-07-01", "2026-07-02").Replace(
        "\"service_version\":\"v1\"",
        "\"service_version\":\"v2\"",
        StringComparison.Ordinal)
    };
    var handler = new StubHandler(_ => JsonResponse(json));

    var exception = await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateClient(handler, new FakeDelay()).FetchRangeAsync(
        new DateOnly(2026, 7, 1),
        new DateOnly(2026, 7, 2),
        CancellationToken.None));

    Assert.Equal(CatalogNasaFailure.InvalidPayload, exception.Failure);
    Assert.Equal(1, handler.CallCount);
  }

  [Theory]
  [InlineData(HttpStatusCode.RequestTimeout)]
  [InlineData(HttpStatusCode.ServiceUnavailable)]
  public async Task FetchRange_TransientStatusRetriesOnceWithBoundedBackoff(
    HttpStatusCode statusCode)
  {
    var handler = new StubHandler(call => call == 1
      ? new HttpResponseMessage(statusCode)
      : JsonResponse(Payload("2026-07-01")));
    var delay = new FakeDelay();

    var result = await CreateClient(handler, delay).FetchRangeAsync(
      new DateOnly(2026, 7, 1),
      new DateOnly(2026, 7, 1),
      CancellationToken.None);

    Assert.Single(result);
    Assert.Equal(2, handler.CallCount);
    Assert.Equal([TimeSpan.FromMilliseconds(250)], delay.Delays);
  }

  [Fact]
  public async Task FetchRange_NetworkFailureRetriesOnce()
  {
    var handler = new StubHandler(call => call == 1
      ? throw new HttpRequestException("sensitive network detail")
      : JsonResponse(Payload("2026-07-01")));
    var delay = new FakeDelay();

    var result = await CreateClient(handler, delay).FetchRangeAsync(
      new DateOnly(2026, 7, 1),
      new DateOnly(2026, 7, 1),
      CancellationToken.None);

    Assert.Single(result);
    Assert.Equal(2, handler.CallCount);
    Assert.Single(delay.Delays);
  }

  [Fact]
  public async Task FetchRange_ExhaustedTransientFailureStopsAfterTwoAttempts()
  {
    var handler = new StubHandler(_ =>
      throw new HttpRequestException("sensitive network detail"));
    var delay = new FakeDelay();

    var exception = await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateClient(handler, delay).FetchRangeAsync(
        new DateOnly(2026, 7, 1),
        new DateOnly(2026, 7, 1),
        CancellationToken.None));

    Assert.Equal(CatalogNasaFailure.Transient, exception.Failure);
    Assert.Equal("The APOD catalog provider request failed.", exception.Message);
    Assert.DoesNotContain("sensitive", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    Assert.Equal(2, handler.CallCount);
    Assert.Single(delay.Delays);
  }

  [Fact]
  public async Task FetchRange_RateLimitReportsRetryAfterWithoutDelayOrRetry()
  {
    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(12));
    var handler = new StubHandler(_ => response);
    var delay = new FakeDelay();

    var exception = await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateClient(handler, delay).FetchRangeAsync(
        new DateOnly(2026, 7, 1),
        new DateOnly(2026, 7, 1),
        CancellationToken.None));

    Assert.Equal(CatalogNasaFailure.RateLimited, exception.Failure);
    Assert.InRange(
      exception.RetryNotBefore!.Value,
      DateTimeOffset.UtcNow.AddMinutes(11),
      DateTimeOffset.UtcNow.AddMinutes(13));
    Assert.Equal(1, handler.CallCount);
    Assert.Empty(delay.Delays);
  }

  [Fact]
  public async Task FetchRange_RateLimitWithoutHeaderUsesOneHourSafetyWindow()
  {
    var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    var handler = new StubHandler(_ =>
      new HttpResponseMessage(HttpStatusCode.TooManyRequests));

    var exception = await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateClient(
        handler,
        new FakeDelay(),
        timeProvider: new FixedTimeProvider(now)).FetchRangeAsync(
          new DateOnly(2026, 7, 1),
          new DateOnly(2026, 7, 1),
          CancellationToken.None));

    Assert.Equal(now.AddHours(1), exception.RetryNotBefore);
    Assert.Equal(1, handler.CallCount);
  }

  [Fact]
  public async Task FetchRange_RateLimitHttpDateUsesAbsoluteRetryWindow()
  {
    var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    var retryAt = now.AddMinutes(20);
    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAt);
    var handler = new StubHandler(_ => response);

    var exception = await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateClient(
        handler,
        new FakeDelay(),
        timeProvider: new FixedTimeProvider(now)).FetchRangeAsync(
          new DateOnly(2026, 7, 1),
          new DateOnly(2026, 7, 1),
          CancellationToken.None));

    Assert.Equal(retryAt, exception.RetryNotBefore);
    Assert.Equal(1, handler.CallCount);
  }

  [Fact]
  public async Task FetchRange_RedirectIsNotFollowedOrRetried()
  {
    var response = new HttpResponseMessage(HttpStatusCode.Found);
    response.Headers.Location = new Uri("https://unexpected.example/collect");
    var handler = new StubHandler(_ => response);

    var exception = await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateClient(handler, new FakeDelay()).FetchRangeAsync(
        new DateOnly(2026, 7, 1),
        new DateOnly(2026, 7, 1),
        CancellationToken.None));

    Assert.Equal(CatalogNasaFailure.Permanent, exception.Failure);
    Assert.Equal(1, handler.CallCount);
  }

  private static NasaCatalogClient CreateClient(
    HttpMessageHandler handler,
    ICatalogRetryDelay delay,
    string apiKey = "test-key",
    TimeProvider? timeProvider = null) => new(
      new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov/") },
      apiKey,
      delay,
      timeProvider ?? TimeProvider.System);

  private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
  {
    Content = new StringContent(json, Encoding.UTF8, "application/json")
  };

  private static string Payload(params string[] dates) =>
    "[" + string.Join(
      ",",
      dates.Select(date => $$"""
        {"date":"{{date}}","title":"APOD {{date}}","explanation":"Explanation",
        "media_type":"image","url":"https://images.example/{{date}}.jpg",
        "hdurl":null,"thumbnail_url":" ","copyright":null,"service_version":"v1"}
        """)) + "]";

  private sealed class FakeDelay : ICatalogRetryDelay
  {
    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Delays.Add(delay);
      return Task.CompletedTask;
    }
  }

  private sealed class StubHandler(Func<int, HttpResponseMessage> responseFactory)
    : HttpMessageHandler
  {
    public int CallCount { get; private set; }

    public Uri? RequestUri { get; private set; }

    public string? ApiKey { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      CallCount++;
      RequestUri = request.RequestUri;
      ApiKey = request.Headers.GetValues("X-Api-Key").Single();
      return Task.FromResult(responseFactory(CallCount));
    }
  }

  private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => utcNow;
  }
}
