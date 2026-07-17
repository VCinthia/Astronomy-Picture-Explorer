using System.Net;
using System.Text;
using System.Text.Json;
using AstronomyExplorer.Api.Nasa;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Tests.Apod;

public sealed class NasaApodClientTests
{
  private static readonly DateOnly RequestedDate = new(2026, 7, 10);

  [Fact]
  public async Task GetByDate_ImagePayload_MapsStableContractAndSecuresApiKey()
  {
    const string apiKey = "secret-test-key";
    var handler = new StubHandler(_ => JsonResponse("""
      {
        "date": "2026-07-10",
        "title": "  A nebula  ",
        "explanation": "  Stellar nursery.  ",
        "media_type": "image",
        "url": " https://images.example/apod.jpg ",
        "hdurl": " https://images.example/apod-hd.jpg ",
        "thumbnail_url": "   ",
        "copyright": "  NASA  ",
        "service_version": "v1",
        "resource": { "image_set": "apod" }
      }
      """));
    var client = CreateClient(handler, apiKey);

    var result = await client.GetByDateAsync(RequestedDate, CancellationToken.None);

    Assert.Equal("A nebula", result.Title);
    Assert.Equal("Stellar nursery.", result.Explanation);
    Assert.Equal("https://images.example/apod.jpg", result.Url);
    Assert.Equal("https://images.example/apod-hd.jpg", result.HdUrl);
    Assert.Null(result.ThumbnailUrl);
    Assert.Equal("NASA", result.Copyright);
    Assert.Equal(1, handler.CallCount);
    Assert.Equal(apiKey, handler.ApiKey);
    Assert.Equal("?date=2026-07-10&thumbs=true", handler.RequestUri?.Query);
    Assert.DoesNotContain("api_key", handler.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);

    var json = JsonSerializer.Serialize(result);
    using var document = JsonDocument.Parse(json);
    Assert.Equal(
      ["copyright", "date", "explanation", "hdurl", "media_type", "thumbnail_url", "title", "url"],
      document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
    Assert.DoesNotContain("service_version", json, StringComparison.Ordinal);
  }

  [Fact]
  public async Task GetByDate_VideoPayload_NormalizesMissingOptionals()
  {
    var handler = new StubHandler(_ => JsonResponse(ValidPayload(
      mediaType: "video",
      optionals: "\"hdurl\": null, \"thumbnail_url\": \"\", \"copyright\": \" \",")));

    var result = await CreateClient(handler).GetByDateAsync(
      RequestedDate,
      CancellationToken.None);

    Assert.Equal("video", result.MediaType);
    Assert.Null(result.HdUrl);
    Assert.Null(result.ThumbnailUrl);
    Assert.Null(result.Copyright);
  }

  [Fact]
  public async Task GetByDate_OmittedOptionalProperties_MapToNull()
  {
    var handler = new StubHandler(_ => JsonResponse(ValidPayload(optionals: string.Empty)));

    var result = await CreateClient(handler).GetByDateAsync(
      RequestedDate,
      CancellationToken.None);

    Assert.Null(result.HdUrl);
    Assert.Null(result.ThumbnailUrl);
    Assert.Null(result.Copyright);
  }

  [Theory]
  [InlineData("\"date\": \"2026-07-11\"")]
  [InlineData("\"title\": \"   \"")]
  [InlineData("\"media_type\": \"audio\"")]
  [InlineData("\"url\": \"/relative.jpg\"")]
  [InlineData("\"hdurl\": \"ftp://images.example/apod.jpg\"")]
  [InlineData("\"service_version\": \"v2\"")]
  [InlineData("\"service_version\": \"   \"")]
  public async Task GetByDate_InvalidProviderContract_RejectsPayload(string replacement)
  {
    var property = replacement[..replacement.IndexOf(':')];
    var payload = ValidPayload().Replace(
      property switch
      {
        "\"date\"" => "\"date\": \"2026-07-10\"",
        "\"title\"" => "\"title\": \"APOD\"",
        "\"media_type\"" => "\"media_type\": \"image\"",
        "\"url\"" => "\"url\": \"https://images.example/apod.jpg\"",
        "\"hdurl\"" => "\"hdurl\": \"https://images.example/apod-hd.jpg\"",
        _ => "\"service_version\": \"v1\""
      },
      replacement,
      StringComparison.Ordinal);
    var handler = new StubHandler(_ => JsonResponse(payload));

    var exception = await Assert.ThrowsAsync<NasaApodException>(() =>
      CreateClient(handler).GetByDateAsync(RequestedDate, CancellationToken.None));

    Assert.Equal(NasaApodFailure.InvalidPayload, exception.Failure);
  }

  [Theory]
  [InlineData("{ invalid-json")]
  [InlineData("{ \"date\": \"2026-07-10\" }")]
  public async Task GetByDate_InvalidJsonOrMissingServiceVersion_RejectsPayload(string payload)
  {
    var handler = new StubHandler(_ => JsonResponse(payload));

    var exception = await Assert.ThrowsAsync<NasaApodException>(() =>
      CreateClient(handler).GetByDateAsync(RequestedDate, CancellationToken.None));

    Assert.Equal(NasaApodFailure.InvalidPayload, exception.Failure);
    Assert.Equal(1, handler.CallCount);
  }

  [Fact]
  public async Task GetByDate_ServerError_RetriesOnceThenSucceeds()
  {
    var handler = new StubHandler(call => call == 1
      ? new HttpResponseMessage(HttpStatusCode.BadGateway)
      : JsonResponse(ValidPayload()));

    var result = await CreateClient(handler).GetByDateAsync(
      RequestedDate,
      CancellationToken.None);

    Assert.Equal(RequestedDate, result.Date);
    Assert.Equal(2, handler.CallCount);
  }

  [Fact]
  public async Task GetByDate_RepeatedServerError_ReturnsSanitizedFailure()
  {
    const string apiKey = "must-not-leak";
    var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
    {
      Content = new StringContent("private upstream details")
    });

    var exception = await Assert.ThrowsAsync<NasaApodException>(() =>
      CreateClient(handler, apiKey).GetByDateAsync(RequestedDate, CancellationToken.None));

    Assert.Equal(NasaApodFailure.Upstream, exception.Failure);
    Assert.Equal(2, handler.CallCount);
    Assert.DoesNotContain(apiKey, exception.ToString(), StringComparison.Ordinal);
    Assert.DoesNotContain("private upstream details", exception.ToString(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task GetByDate_RateLimited_DoesNotSpendQuotaOnRetry()
  {
    var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

    var exception = await Assert.ThrowsAsync<NasaApodException>(() =>
      CreateClient(handler).GetByDateAsync(RequestedDate, CancellationToken.None));

    Assert.Equal(NasaApodFailure.RateLimited, exception.Failure);
    Assert.Equal(1, handler.CallCount);
  }

  [Fact]
  public async Task GetByDate_Redirect_IsControlledFailureAndNeverFollowed()
  {
    var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
    {
      Headers = { Location = new Uri("https://unexpected.example/collect") }
    });

    var exception = await Assert.ThrowsAsync<NasaApodException>(() =>
      CreateClient(handler).GetByDateAsync(RequestedDate, CancellationToken.None));

    Assert.Equal(NasaApodFailure.Upstream, exception.Failure);
    Assert.Equal(1, handler.CallCount);
  }

  [Fact]
  public void PrimaryHandler_DisablesAutomaticRedirects()
  {
    using var handler = NasaApodHttpClientConfiguration.CreatePrimaryHandler();

    Assert.False(handler.AllowAutoRedirect);
  }

  [Fact]
  public async Task GetByDate_TimeoutTwice_ReturnsSanitizedFailure()
  {
    const string apiKey = "must-not-leak";
    var handler = new StubHandler(_ => throw new TaskCanceledException("provider body and query"));

    var exception = await Assert.ThrowsAsync<NasaApodException>(() =>
      CreateClient(handler, apiKey).GetByDateAsync(RequestedDate, CancellationToken.None));

    Assert.Equal(NasaApodFailure.Timeout, exception.Failure);
    Assert.Equal("The APOD provider request failed.", exception.Message);
    Assert.DoesNotContain(apiKey, exception.ToString(), StringComparison.Ordinal);
    Assert.DoesNotContain("provider body", exception.ToString(), StringComparison.Ordinal);
    Assert.Equal(2, handler.CallCount);
  }

  [Fact]
  public async Task GetByDate_CallerCancellation_PropagatesWithoutRetry()
  {
    var handler = new StubHandler(_ => throw new OperationCanceledException());
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      CreateClient(handler).GetByDateAsync(RequestedDate, cancellation.Token));

    Assert.Equal(1, handler.CallCount);
  }

  private static NasaApodClient CreateClient(
    HttpMessageHandler handler,
    string apiKey = "test-key")
  {
    var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.nasa.gov/")
    };
    return new NasaApodClient(
      httpClient,
      Options.Create(new NasaApodOptions { ApiKey = apiKey, MaxAttempts = 2 }));
  }

  private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
  {
    Content = new StringContent(json, Encoding.UTF8, "application/json")
  };

  private static string ValidPayload(
    string mediaType = "image",
    string optionals = "\"hdurl\": \"https://images.example/apod-hd.jpg\",") => $$"""
    {
      "date": "2026-07-10",
      "title": "APOD",
      "explanation": "Explanation",
      "media_type": "{{mediaType}}",
      "url": "https://images.example/apod.jpg",
      {{optionals}}
      "service_version": "v1"
    }
    """;

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
}
