using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Nasa;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace AstronomyExplorer.Api.Tests.Apod.Search;

[Collection(PostgreSqlCollection.Name)]
public sealed class ApodSearchEndpointTests(PostgreSqlFixture database) : IDisposable
{
  private static readonly DateOnly TargetFrom = new(2014, 1, 1);
  private static readonly DateOnly TargetTo = new(2014, 1, 31);
  private readonly CancellationTokenSource _testCancellation =
    new(TimeSpan.FromSeconds(30));

  [Fact]
  public async Task Search_TitleMatchRanksAboveExplanationAndReturnsAppOwnedArray()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(5, "Nebula frontier", "A quiet deep-sky observation."),
      Entry(4, "Dust frontier", "A nebula observed in the southern sky."));
    var provider = new CountingNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/api/apod/search?q=%20nebula%20", cancellationToken);
    using var document = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync(cancellationToken));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    Assert.Equal("Nebula frontier", document.RootElement[0].GetProperty("title").GetString());
    Assert.Equal(
      ["copyright", "date", "explanation", "hdurl", "media_type", "thumbnail_url", "title", "url"],
      document.RootElement[0].EnumerateObject().Select(property => property.Name).Order().ToArray());
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Search_EnglishStemmingMatchesInflectedWord()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(6, "Spiral galaxies", "A distant cluster."));
    using var factory = CreateFactory(new CountingNasaApodClient());
    using var client = factory.CreateClient();

    var results = await client.GetFromJsonAsync<ApodEntryDto[]>(
      "/api/apod/search?q=galaxy",
      cancellationToken);

    var result = Assert.Single(Assert.IsType<ApodEntryDto[]>(results));
    Assert.Equal("Spiral galaxies", result.Title);
  }

  [Fact]
  public async Task Search_WebSyntaxHandlesSpecialCharactersWithoutSqlInjection()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(7, "A black hole portrait", "Deep gravity without foreground stars."),
      Entry(8, "Nebula stars", "A bright stellar nursery."));
    using var factory = CreateFactory(new CountingNasaApodClient());
    using var client = factory.CreateClient();
    var query = Uri.EscapeDataString("\"black hole\" OR nebula -stars");
    var injection = Uri.EscapeDataString("nebula'); DROP TABLE apod_entries; --");

    var specialResults = await client.GetFromJsonAsync<ApodEntryDto[]>(
      $"/api/apod/search?q={query}",
      cancellationToken);
    using var injectionResponse = await client.GetAsync(
      $"/api/apod/search?q={injection}",
      cancellationToken);
    await using var context = database.CreateDbContext();

    var result = Assert.Single(Assert.IsType<ApodEntryDto[]>(specialResults));
    Assert.Equal("A black hole portrait", result.Title);
    Assert.Equal(HttpStatusCode.OK, injectionResponse.StatusCode);
    Assert.True(await context.ApodEntries.AnyAsync(
      entry => entry.Date == TargetFrom.AddDays(7),
      cancellationToken));
  }

  [Fact]
  public async Task Search_PaginationUsesStableRankThenDescendingDate()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(9, "Cosmos", "Deep field."),
      Entry(10, "Cosmos", "Deep field."),
      Entry(11, "Cosmos", "Deep field."),
      Entry(12, "Cosmos", "Deep field."));
    using var factory = CreateFactory(new CountingNasaApodClient());
    using var client = factory.CreateClient();

    var firstPage = await client.GetFromJsonAsync<ApodEntryDto[]>(
      "/api/apod/search?q=cosmos&page=1&pageSize=2",
      cancellationToken);
    var secondPage = await client.GetFromJsonAsync<ApodEntryDto[]>(
      "/api/apod/search?q=cosmos&page=2&pageSize=2",
      cancellationToken);

    Assert.Equal([TargetFrom.AddDays(12), TargetFrom.AddDays(11)], firstPage?.Select(x => x.Date));
    Assert.Equal([TargetFrom.AddDays(10), TargetFrom.AddDays(9)], secondPage?.Select(x => x.Date));
  }

  [Fact]
  public async Task Search_NoMatchOrPartialTypo_ReturnsEmptyArrayWithoutTrigramFallback()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(13, "Carina Nebula", "A colorful cloud."));
    using var factory = CreateFactory(new CountingNasaApodClient());
    using var client = factory.CreateClient();

    var partial = await client.GetFromJsonAsync<ApodEntryDto[]>(
      "/api/apod/search?q=nebul",
      cancellationToken);
    var typo = await client.GetFromJsonAsync<ApodEntryDto[]>(
      "/api/apod/search?q=neubla",
      cancellationToken);

    Assert.Empty(Assert.IsType<ApodEntryDto[]>(partial));
    Assert.Empty(Assert.IsType<ApodEntryDto[]>(typo));
  }

  [Theory]
  [InlineData("/api/apod/search", "invalid_search_query")]
  [InlineData("/api/apod/search?q=%20%20", "invalid_search_query")]
  [InlineData("/api/apod/search?q=nebula&page=0", "invalid_search_pagination")]
  [InlineData("/api/apod/search?q=nebula&pageSize=0", "invalid_search_pagination")]
  [InlineData("/api/apod/search?q=nebula&pageSize=-1", "invalid_search_pagination")]
  [InlineData("/api/apod/search?q=nebula&pageSize=31", "invalid_search_pagination")]
  [InlineData("/api/apod/search?q=nebula&page=1001", "invalid_search_pagination")]
  [InlineData("/api/apod/search?q=nebula&page=2147483647&pageSize=30", "invalid_search_pagination")]
  public async Task Search_InvalidInput_ReturnsStableProblem(string requestUri, string expectedCode)
  {
    var cancellationToken = _testCancellation.Token;
    using var factory = CreateFactory(new CountingNasaApodClient());
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(requestUri, cancellationToken);
    using var problem = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync(cancellationToken));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());
  }

  [Fact]
  public async Task Search_QueryLongerThanLimit_ReturnsStableProblem()
  {
    var cancellationToken = _testCancellation.Token;
    using var factory = CreateFactory(new CountingNasaApodClient());
    using var client = factory.CreateClient();
    var query = new string('a', ApodEndpoints.MaxSearchQueryLength + 1);

    using var response = await client.GetAsync(
      $"/api/apod/search?q={query}",
      cancellationToken);
    using var problem = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync(cancellationToken));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("invalid_search_query", problem.RootElement.GetProperty("code").GetString());
  }

  [Fact]
  public async Task Search_CanonicalCatalogNotReady_ReturnsServiceUnavailableWithoutNasaCall()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(14, "Unready Nebula", "Should not be returned."));
    await using (var context = database.CreateDbContext())
    {
      var state = await context.CatalogSyncStates.SingleAsync(
        item => item.TargetFrom == TargetFrom && item.TargetTo == TargetTo,
        cancellationToken);
      state.Status = CatalogSyncStatus.Paused;
      state.LastCompletedDate = TargetTo.AddDays(-1);
      await context.SaveChangesAsync(cancellationToken);
    }

    var provider = new CountingNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/api/apod/search?q=nebula", cancellationToken);
    using var problem = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync(cancellationToken));

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("catalog_not_ready", problem.RootElement.GetProperty("code").GetString());
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Search_WithoutConfiguredTarget_ReturnsCatalogNotReady()
  {
    var cancellationToken = _testCancellation.Token;
    var provider = new CountingNasaApodClient();
    using var factory = CreateFactory(provider, configureCatalogTarget: false);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/api/apod/search?q=nebula", cancellationToken);
    using var problem = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync(cancellationToken));

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("catalog_not_ready", problem.RootElement.GetProperty("code").GetString());
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Search_CompletedCatalogWithCoverageDrift_ReturnsCatalogNotReady()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(16, "First Nebula", "Coverage evidence."),
      Entry(17, "Second Nebula", "Coverage evidence."));
    await using (var context = database.CreateDbContext())
    {
      await context.ApodEntries
        .Where(entry => entry.Date == TargetFrom.AddDays(17))
        .ExecuteDeleteAsync(cancellationToken);
    }

    var provider = new CountingNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/api/apod/search?q=nebula", cancellationToken);
    using var problem = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync(cancellationToken));

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("catalog_not_ready", problem.RootElement.GetProperty("code").GetString());
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Search_QueryPlanUsesStoredVectorGinIndex()
  {
    var cancellationToken = _testCancellation.Token;
    await SeedReadyCatalogAsync(
      cancellationToken,
      Entry(15, "Indexed Nebula", "Search plan evidence."));
    await using var connection = new NpgsqlConnection(database.ConnectionString);
    await connection.OpenAsync(cancellationToken);
    await using (var disableSequentialScan = new NpgsqlCommand("SET enable_seqscan = off;", connection))
    {
      await disableSequentialScan.ExecuteNonQueryAsync(cancellationToken);
    }

    const string explainSql =
      """
      EXPLAIN (COSTS OFF)
      SELECT date
      FROM apod_entries
      WHERE search_vector @@ websearch_to_tsquery('english', @query);
      """;
    await using var command = new NpgsqlCommand(explainSql, connection);
    command.Parameters.AddWithValue("query", "nebula");
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var plan = new List<string>();
    while (await reader.ReadAsync(cancellationToken))
    {
      plan.Add(reader.GetString(0));
    }

    Assert.Contains(
      plan,
      line => line.Contains("ix_apod_entries_search_vector", StringComparison.Ordinal));
  }

  public void Dispose() => _testCancellation.Dispose();

  private ApodSearchApiFactory CreateFactory(
    CountingNasaApodClient provider,
    bool configureCatalogTarget = true) =>
    new(database.ConnectionString, provider, configureCatalogTarget);

  private async Task SeedReadyCatalogAsync(
    CancellationToken cancellationToken,
    params ApodEntry[] entries)
  {
    await using var context = database.CreateDbContext();
    await context.Favorites
      .Where(favorite => favorite.ApodDate >= TargetFrom && favorite.ApodDate <= TargetTo)
      .ExecuteDeleteAsync(cancellationToken);
    await context.ApodEntries
      .Where(entry => entry.Date >= TargetFrom && entry.Date <= TargetTo)
      .ExecuteDeleteAsync(cancellationToken);
    await context.CatalogSyncStates
      .Where(state => state.TargetFrom == TargetFrom && state.TargetTo == TargetTo)
      .ExecuteDeleteAsync(cancellationToken);
    context.ApodEntries.AddRange(entries);
    context.CatalogSyncStates.Add(new CatalogSyncState
    {
      Id = Guid.NewGuid(),
      TargetFrom = TargetFrom,
      TargetTo = TargetTo,
      LastCompletedDate = TargetTo,
      SyncedEntryCount = entries.Length,
      Status = CatalogSyncStatus.Completed,
      CreatedAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero),
      UpdatedAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
    });
    await context.SaveChangesAsync(cancellationToken);
  }

  private static ApodEntry Entry(int dayOffset, string title, string explanation) => new()
  {
    Date = TargetFrom.AddDays(dayOffset),
    Title = title,
    Explanation = explanation,
    MediaType = "image",
    Url = $"https://images.example/{dayOffset}.jpg",
    CachedAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
  };

  private sealed class CountingNasaApodClient : INasaApodClient
  {
    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    public Task<ApodEntryDto> GetByDateAsync(
      DateOnly date,
      CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref _callCount);
      return Task.FromException<ApodEntryDto>(
        new InvalidOperationException("APOD search must not call NASA."));
    }
  }

  private sealed class ApodSearchApiFactory(
    string connectionString,
    CountingNasaApodClient provider,
    bool configureCatalogTarget) : WebApplicationFactory<Program>
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
      if (configureCatalogTarget)
      {
        builder.UseSetting("Catalog:RequiredFrom", $"{TargetFrom:yyyy-MM-dd}");
        builder.UseSetting("Catalog:RequiredTo", $"{TargetTo:yyyy-MM-dd}");
      }
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<INasaApodClient>();
        services.AddSingleton<INasaApodClient>(provider);
      });
    }
  }
}
