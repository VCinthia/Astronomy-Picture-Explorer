using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Nasa;
using AstronomyExplorer.Api.Tests.Auth.Sessions;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AstronomyExplorer.Api.Tests.Apod;

[Collection(PostgreSqlCollection.Name)]
public sealed class ApodCacheEndpointTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task GetByDate_CacheMiss_PersistsAndReusesEntryAcrossAppInstances()
  {
    var date = new DateOnly(2025, 6, 1);
    await DeleteEntryAsync(date);
    var firstProvider = new FakeNasaApodClient();

    using (var firstFactory = CreateFactory(firstProvider))
    using (var client = CreateClient(firstFactory))
    {
      var first = await client.GetFromJsonAsync<ApodEntryDto>($"/api/apod/date/{date:yyyy-MM-dd}");
      var repeated = await client.GetFromJsonAsync<ApodEntryDto>($"/api/apod/date/{date:yyyy-MM-dd}");

      Assert.Equal(date, first?.Date);
      Assert.Equal(first, repeated);
      Assert.Equal(1, firstProvider.CallCount);
    }

    await using (var context = database.CreateDbContext())
    {
      var persisted = await context.ApodEntries.AsNoTracking().SingleAsync(entry => entry.Date == date);
      Assert.Equal("APOD 2025-06-01", persisted.Title);
    }

    var secondProvider = new FakeNasaApodClient((_, _, _) =>
      throw new InvalidOperationException("PostgreSQL should satisfy the request."));
    using var secondFactory = CreateFactory(secondProvider);
    using var secondClient = CreateClient(secondFactory);

    var restored = await secondClient.GetFromJsonAsync<ApodEntryDto>(
      $"/api/apod/date/{date:yyyy-MM-dd}");

    Assert.Equal(date, restored?.Date);
    Assert.Equal(0, secondProvider.CallCount);
  }

  [Fact]
  public async Task GetByDate_ConcurrentMisses_UseSingleProviderCall()
  {
    var date = new DateOnly(2025, 6, 2);
    await DeleteEntryAsync(date);
    var provider = new FakeNasaApodClient(async (_, requestedDate, cancellationToken) =>
    {
      await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
      return CreateEntry(requestedDate);
    });
    using var factory = CreateFactory(provider);
    using var client = CreateClient(factory);

    var requests = Enumerable.Range(0, 12)
      .Select(_ => client.GetAsync($"/api/apod/date/{date:yyyy-MM-dd}"));
    var responses = await Task.WhenAll(requests);

    Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    Assert.Equal(1, provider.CallCount);
  }

  [Fact]
  public async Task GetByDate_FailedMissIsNotCachedAndRetryCanRecover()
  {
    var date = new DateOnly(2025, 6, 3);
    await DeleteEntryAsync(date);
    var provider = new FakeNasaApodClient((call, requestedDate, _) => call == 1
      ? Task.FromException<ApodEntryDto>(
          new NasaApodException(NasaApodFailure.Upstream))
      : Task.FromResult(CreateEntry(requestedDate)));
    using var factory = CreateFactory(provider);
    using var client = CreateClient(factory);

    var failed = await client.GetAsync($"/api/apod/date/{date:yyyy-MM-dd}");
    var recovered = await client.GetAsync($"/api/apod/date/{date:yyyy-MM-dd}");

    Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
    Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    Assert.Equal(2, provider.CallCount);
  }

  [Theory]
  [InlineData("1995-06-15")]
  [InlineData("2026-07-11")]
  [InlineData("2026-7-1")]
  [InlineData("not-a-date")]
  public async Task GetByDate_InvalidDate_ReturnsStableProblemWithoutProviderCall(string date)
  {
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(
      provider,
      new MutableTimeProvider(new DateTimeOffset(2026, 7, 10, 23, 59, 0, TimeSpan.Zero)));
    using var client = CreateClient(factory);

    var response = await client.GetAsync($"/api/apod/date/{date}");
    using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("invalid_apod_date", problem.RootElement.GetProperty("code").GetString());
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task GetToday_BeforeArgentinaMidnight_ReturnsArgentinaDateAndExactAppOwnedShape()
  {
    var date = new DateOnly(2026, 8, 12);
    await DeleteEntryAsync(date);
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(
      provider,
      new MutableTimeProvider(new DateTimeOffset(2026, 8, 13, 2, 59, 59, TimeSpan.Zero)));
    using var client = CreateClient(factory);

    var response = await client.GetAsync("/api/apod/today");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("2026-08-12", document.RootElement.GetProperty("date").GetString());
    Assert.Equal(
      ["copyright", "date", "explanation", "hdurl", "media_type", "thumbnail_url", "title", "url"],
      document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
    Assert.Equal(1, provider.CallCount);
  }

  [Fact]
  public async Task GetToday_AtArgentinaMidnight_ReturnsNewArgentinaDate()
  {
    var date = new DateOnly(2026, 8, 13);
    await DeleteEntryAsync(date);
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(
      provider,
      new MutableTimeProvider(new DateTimeOffset(2026, 8, 13, 3, 0, 0, TimeSpan.Zero)));
    using var client = CreateClient(factory);

    var response = await client.GetAsync("/api/apod/today");
    var entry = await response.Content.ReadFromJsonAsync<ApodEntryDto>();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(date, entry?.Date);
    Assert.Equal(1, provider.CallCount);
  }

  [Fact]
  public async Task GetByDate_NextArgentinaDateBeforeMidnight_ReturnsStableProblemWithoutProviderCall()
  {
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(
      provider,
      new MutableTimeProvider(new DateTimeOffset(2026, 8, 13, 2, 59, 59, TimeSpan.Zero)));
    using var client = CreateClient(factory);

    var response = await client.GetAsync("/api/apod/date/2026-08-13");
    using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("invalid_apod_date", problem.RootElement.GetProperty("code").GetString());
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task GetByDate_NewArgentinaDateAtMidnight_ContinuesToProvider()
  {
    var date = new DateOnly(2026, 8, 13);
    await DeleteEntryAsync(date);
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(
      provider,
      new MutableTimeProvider(new DateTimeOffset(2026, 8, 13, 3, 0, 0, TimeSpan.Zero)));
    using var client = CreateClient(factory);

    var response = await client.GetAsync($"/api/apod/date/{date:yyyy-MM-dd}");
    var entry = await response.Content.ReadFromJsonAsync<ApodEntryDto>();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(date, entry?.Date);
    Assert.Equal(1, provider.CallCount);
  }

  [Theory]
  [InlineData(NasaApodFailure.RateLimited, HttpStatusCode.ServiceUnavailable, "apod_upstream_unavailable")]
  [InlineData(NasaApodFailure.Timeout, HttpStatusCode.GatewayTimeout, "apod_upstream_timeout")]
  [InlineData(NasaApodFailure.Upstream, HttpStatusCode.BadGateway, "apod_upstream_error")]
  [InlineData(NasaApodFailure.InvalidPayload, HttpStatusCode.BadGateway, "apod_invalid_payload")]
  public async Task GetByDate_ProviderFailure_ReturnsSanitizedProblem(
    NasaApodFailure failure,
    HttpStatusCode expectedStatus,
    string expectedCode)
  {
    var date = new DateOnly(2025, 6, 5).AddDays((int)failure);
    await DeleteEntryAsync(date);
    var provider = new FakeNasaApodClient((_, _, _) =>
      Task.FromException<ApodEntryDto>(new NasaApodException(failure)));
    using var factory = CreateFactory(provider);
    using var client = CreateClient(factory);

    var response = await client.GetAsync($"/api/apod/date/{date:yyyy-MM-dd}");
    var body = await response.Content.ReadAsStringAsync();
    using var problem = JsonDocument.Parse(body);

    Assert.Equal(expectedStatus, response.StatusCode);
    Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());
    Assert.DoesNotContain("test-nasa-api-key", body, StringComparison.Ordinal);
    Assert.DoesNotContain("api_key", body, StringComparison.OrdinalIgnoreCase);
  }

  private ApodApiFactory CreateFactory(
    FakeNasaApodClient provider,
    TimeProvider? timeProvider = null) => new(
      database.ConnectionString,
      provider,
      timeProvider ?? new MutableTimeProvider(
        new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)));

  private static HttpClient CreateClient(ApodApiFactory factory) => factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  private async Task DeleteEntryAsync(DateOnly date)
  {
    await using var context = database.CreateDbContext();
    await context.ApodEntries.Where(entry => entry.Date == date).ExecuteDeleteAsync();
  }

  private static ApodEntryDto CreateEntry(DateOnly date) => new(
    date,
    $"APOD {date:yyyy-MM-dd}",
    "A cached astronomy picture.",
    "image",
    "https://images.example/apod.jpg",
    "https://images.example/apod-hd.jpg",
    null,
    null);

  public sealed class FakeNasaApodClient : INasaApodClient
  {
    private readonly Func<int, DateOnly, CancellationToken, Task<ApodEntryDto>> _response;
    private int _callCount;

    public FakeNasaApodClient(
      Func<int, DateOnly, CancellationToken, Task<ApodEntryDto>>? response = null)
    {
      _response = response ?? ((_, date, _) => Task.FromResult(CreateEntry(date)));
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public Task<ApodEntryDto> GetByDateAsync(
      DateOnly date,
      CancellationToken cancellationToken)
    {
      var call = Interlocked.Increment(ref _callCount);
      return _response(call, date, cancellationToken);
    }
  }

  public sealed class ApodApiFactory(
    string connectionString,
    FakeNasaApodClient provider,
    TimeProvider timeProvider) : WebApplicationFactory<Program>
  {
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Testing");
      builder.UseSetting("ConnectionStrings:Postgres", connectionString);
      builder.UseSetting("Frontend:PublicBaseUrl", "https://portfolio.example");
      builder.UseSetting("Session:Issuer", "https://api.example.test");
      builder.UseSetting("Session:Audience", "astronomy-explorer-tests");
      builder.UseSetting("Session:SigningKey", "test-signing-key-at-least-32-bytes-long");
      builder.UseSetting("NasaApod:ApiKey", "test-nasa-api-key");
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<INasaApodClient>();
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<INasaApodClient>(provider);
        services.AddSingleton(timeProvider);
      });
    }
  }
}
