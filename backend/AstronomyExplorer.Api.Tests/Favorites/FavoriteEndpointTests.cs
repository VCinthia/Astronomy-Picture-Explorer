using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Nasa;
using AstronomyExplorer.Api.Tests.Auth.Sessions;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AstronomyExplorer.Api.Tests.Favorites;

[Collection(PostgreSqlCollection.Name)]
public sealed class FavoriteEndpointTests(PostgreSqlFixture database)
{
  private static readonly DateTimeOffset CurrentTime =
    new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Endpoints_WithoutBearer_ReturnUnauthorized()
  {
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var get = await client.GetAsync("/api/favorites");
    using var post = await client.PostAsJsonAsync(
      "/api/favorites",
      new { apod_date = "2025-01-01" });
    using var delete = await client.DeleteAsync("/api/favorites/2025-01-01");

    Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Post_CacheMissUsesClaimUserAndReturnsNoContent()
  {
    var apodDate = new DateOnly(2025, 1, 2);
    var authenticatedUserId = await CreateUserAsync("favorite-owner");
    var ignoredClientUserId = await CreateUserAsync("favorite-ignored");
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await SendAsync(
      client,
      HttpMethod.Post,
      "/api/favorites",
      CreateAccessToken(authenticatedUserId),
      new { apod_date = apodDate, user_id = ignoredClientUserId });

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(1, provider.CallCount);
    await using var context = database.CreateDbContext();
    Assert.True(await context.ApodEntries.AnyAsync(entry => entry.Date == apodDate));
    Assert.True(await context.Favorites.AnyAsync(
      favorite => favorite.UserId == authenticatedUserId && favorite.ApodDate == apodDate));
    Assert.False(await context.Favorites.AnyAsync(
      favorite => favorite.UserId == ignoredClientUserId && favorite.ApodDate == apodDate));
  }

  [Fact]
  public async Task Post_InvalidDate_ReturnsProblemBeforeCacheOrProvider()
  {
    var userId = await CreateUserAsync("invalid-favorite-date");
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var beforeFirst = await SendAsync(
      client,
      HttpMethod.Post,
      "/api/favorites",
      CreateAccessToken(userId),
      new { apod_date = "1995-06-15" });
    using var afterToday = await SendAsync(
      client,
      HttpMethod.Post,
      "/api/favorites",
      CreateAccessToken(userId),
      new { apod_date = "2026-07-21" });

    Assert.Equal(HttpStatusCode.BadRequest, beforeFirst.StatusCode);
    Assert.Equal("invalid_favorite_apod_date", await ReadProblemCodeAsync(beforeFirst));
    Assert.Equal(HttpStatusCode.BadRequest, afterToday.StatusCode);
    Assert.Equal("invalid_favorite_apod_date", await ReadProblemCodeAsync(afterToday));
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Post_MissingApodDate_ReturnsProblemBeforeCacheOrProvider()
  {
    var userId = await CreateUserAsync("missing-favorite-date");
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await SendAsync(
      client,
      HttpMethod.Post,
      "/api/favorites",
      CreateAccessToken(userId),
      new { });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("invalid_favorite_apod_date", await ReadProblemCodeAsync(response));
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Post_ProviderFailure_ReturnsExistingSanitizedApodProblem()
  {
    var userId = await CreateUserAsync("failed-favorite-cache");
    var provider = new FakeNasaApodClient((_, _) =>
      Task.FromException<ApodEntryDto>(new NasaApodException(NasaApodFailure.Timeout)));
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await SendAsync(
      client,
      HttpMethod.Post,
      "/api/favorites",
      CreateAccessToken(userId),
      new { apod_date = "2025-01-08" });

    Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    Assert.Equal("apod_upstream_timeout", await ReadProblemCodeAsync(response));
    Assert.Equal(1, provider.CallCount);
  }

  [Fact]
  public async Task Post_ConcurrentDuplicates_CreateOneFavoriteAndOneCacheMiss()
  {
    var apodDate = new DateOnly(2025, 1, 3);
    var userId = await CreateUserAsync("concurrent-favorite");
    var provider = new FakeNasaApodClient(async (date, cancellationToken) =>
    {
      await Task.Delay(TimeSpan.FromMilliseconds(75), cancellationToken);
      return CachedEntry(date);
    });
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();
    var token = CreateAccessToken(userId);

    var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => SendAsync(
      client,
      HttpMethod.Post,
      "/api/favorites",
      token,
      new { apod_date = apodDate })));
    using var responseDisposer = new ResponseDisposer(responses);

    Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
    Assert.Equal(1, provider.CallCount);
    await using var context = database.CreateDbContext();
    Assert.Equal(1, await context.Favorites.CountAsync(
      favorite => favorite.UserId == userId && favorite.ApodDate == apodDate));
  }

  [Fact]
  public async Task Get_ReturnsOneHydratedOrderedQueryWithoutNPlusOne()
  {
    var userId = await CreateUserAsync("favorite-list-owner");
    var otherUserId = await CreateUserAsync("favorite-list-other");
    var first = new DateOnly(2025, 1, 4);
    var second = first.AddDays(1);
    var third = second.AddDays(1);
    var otherUserDate = third.AddDays(1);
    await SeedFavoriteAsync(userId, first);
    await SeedFavoriteAsync(userId, second);
    await SeedFavoriteAsync(userId, third);
    await SeedFavoriteAsync(otherUserId, otherUserDate);
    var provider = new FakeNasaApodClient();
    var counter = new DbCommandCounter();
    using var factory = CreateFactory(provider, counter);
    using var client = factory.CreateClient();
    counter.Reset();

    using var response = await SendAsync(
      client,
      HttpMethod.Get,
      "/api/favorites",
      CreateAccessToken(userId));
    var payload = await response.Content.ReadAsStringAsync();
    using var document = JsonDocument.Parse(payload);
    var entries = document.RootElement
      .EnumerateArray()
      .Select(entry => DateOnly.ParseExact(
        entry.GetProperty("date").GetString()!,
        "yyyy-MM-dd"))
      .ToArray();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal([third, second, first], entries);
    Assert.Equal(
      ["copyright", "date", "explanation", "hdurl", "media_type", "thumbnail_url", "title", "url"],
      document.RootElement[0].EnumerateObject().Select(property => property.Name).Order().ToArray());
    Assert.Equal(1, counter.ReaderExecutions);
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task Delete_FiltersByClaimIsIdempotentAndValidatesDate()
  {
    var apodDate = new DateOnly(2025, 1, 7);
    var ownerId = await CreateUserAsync("delete-owner");
    var otherId = await CreateUserAsync("delete-other");
    await SeedFavoriteAsync(ownerId, apodDate);
    await SeedFavoriteAsync(otherId, apodDate);
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var firstDelete = await SendAsync(
      client,
      HttpMethod.Delete,
      $"/api/favorites/{apodDate:yyyy-MM-dd}",
      CreateAccessToken(ownerId));
    using var repeatedDelete = await SendAsync(
      client,
      HttpMethod.Delete,
      $"/api/favorites/{apodDate:yyyy-MM-dd}",
      CreateAccessToken(ownerId));
    using var invalidDate = await SendAsync(
      client,
      HttpMethod.Delete,
      "/api/favorites/not-a-date",
      CreateAccessToken(otherId));

    Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, repeatedDelete.StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, invalidDate.StatusCode);
    Assert.Equal("invalid_favorite_apod_date", await ReadProblemCodeAsync(invalidDate));
    await using var context = database.CreateDbContext();
    Assert.False(await context.Favorites.AnyAsync(
      favorite => favorite.UserId == ownerId && favorite.ApodDate == apodDate));
    Assert.True(await context.Favorites.AnyAsync(
      favorite => favorite.UserId == otherId && favorite.ApodDate == apodDate));
    Assert.Equal(0, provider.CallCount);
  }

  [Fact]
  public async Task SignedTokenWithMalformedSubject_ReturnsAppOwnedUnauthorizedProblem()
  {
    var provider = new FakeNasaApodClient();
    using var factory = CreateFactory(provider);
    using var client = factory.CreateClient();

    using var response = await SendAsync(
      client,
      HttpMethod.Get,
      "/api/favorites",
      CreateAccessToken("not-a-guid"));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal("invalid_authenticated_user", await ReadProblemCodeAsync(response));
  }

  private FavoritesApiFactory CreateFactory(
    FakeNasaApodClient provider,
    DbCommandCounter? commandCounter = null) =>
    new(database.ConnectionString, provider, commandCounter);

  private async Task<Guid> CreateUserAsync(string prefix)
  {
    var id = Guid.NewGuid();
    var email = $"{prefix}-{id:N}@example.test";
    await using var context = database.CreateDbContext();
    context.Users.Add(new ApplicationUser
    {
      Id = id,
      Email = email,
      UserName = email,
      NormalizedEmail = email.ToUpperInvariant(),
      NormalizedUserName = email.ToUpperInvariant(),
      EmailConfirmed = true,
      SecurityStamp = Guid.NewGuid().ToString(),
      ConcurrencyStamp = Guid.NewGuid().ToString()
    });
    await context.SaveChangesAsync();
    return id;
  }

  private async Task SeedFavoriteAsync(Guid userId, DateOnly apodDate)
  {
    await using var context = database.CreateDbContext();
    if (!await context.ApodEntries.AnyAsync(entry => entry.Date == apodDate))
    {
      context.ApodEntries.Add(Entry(apodDate));
    }
    context.Favorites.Add(new Favorite
    {
      UserId = userId,
      ApodDate = apodDate,
      CreatedAt = CurrentTime
    });
    await context.SaveChangesAsync();
  }

  private static Task<HttpResponseMessage> SendAsync(
    HttpClient client,
    HttpMethod method,
    string requestUri,
    string accessToken,
    object? content = null)
  {
    var request = new HttpRequestMessage(method, requestUri)
    {
      Content = content is null ? null : JsonContent.Create(content)
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    return client.SendAsync(request);
  }

  private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
  {
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return document.RootElement.GetProperty("code").GetString();
  }

  private static string CreateAccessToken(Guid userId) => CreateAccessToken(userId.ToString());

  private static string CreateAccessToken(string subject)
  {
    var now = DateTime.UtcNow;
    var credentials = new SigningCredentials(
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SessionApiFactory.SigningKey)),
      SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
      SessionApiFactory.Issuer,
      SessionApiFactory.Audience,
      [new Claim(JwtRegisteredClaimNames.Sub, subject)],
      notBefore: now.AddMinutes(-1),
      expires: now.AddMinutes(10),
      signingCredentials: credentials);
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static ApodEntry Entry(DateOnly date) => new()
  {
    Date = date,
    Title = $"APOD {date:yyyy-MM-dd}",
    Explanation = "A hydrated astronomy picture.",
    MediaType = "image",
    Url = $"https://images.example/{date:yyyy-MM-dd}.jpg",
    CachedAt = CurrentTime
  };

  private static ApodEntryDto CachedEntry(DateOnly date) => new(
    date,
    $"APOD {date:yyyy-MM-dd}",
    "A cached astronomy picture.",
    "image",
    $"https://images.example/{date:yyyy-MM-dd}.jpg",
    null,
    null,
    null);

  private sealed class FakeNasaApodClient(
    Func<DateOnly, CancellationToken, Task<ApodEntryDto>>? response = null) : INasaApodClient
  {
    private readonly Func<DateOnly, CancellationToken, Task<ApodEntryDto>> _response =
      response ?? ((date, _) => Task.FromResult(CachedEntry(date)));
    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    public Task<ApodEntryDto> GetByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref _callCount);
      return _response(date, cancellationToken);
    }
  }

  private sealed class FavoritesApiFactory(
    string connectionString,
    FakeNasaApodClient provider,
    DbCommandCounter? commandCounter) : WebApplicationFactory<Program>
  {
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Testing");
      builder.UseSetting("ConnectionStrings:Postgres", connectionString);
      builder.UseSetting("Frontend:PublicBaseUrl", SessionApiFactory.AllowedOrigin);
      builder.UseSetting("Session:Issuer", SessionApiFactory.Issuer);
      builder.UseSetting("Session:Audience", SessionApiFactory.Audience);
      builder.UseSetting("Session:SigningKey", SessionApiFactory.SigningKey);
      builder.UseSetting("NasaApod:ApiKey", "test-nasa-api-key");
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<INasaApodClient>();
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<INasaApodClient>(provider);
        services.AddSingleton<TimeProvider>(new MutableTimeProvider(CurrentTime));
        if (commandCounter is not null)
        {
          services.RemoveAll<DbContextOptions<AppDbContext>>();
          services.RemoveAll<AppDbContext>();
          services.AddDbContext<AppDbContext>((_, options) =>
            options.UseNpgsql(connectionString).AddInterceptors(commandCounter));
        }
      });
    }
  }

  private sealed class DbCommandCounter : DbCommandInterceptor
  {
    private int _readerExecutions;

    public int ReaderExecutions => Volatile.Read(ref _readerExecutions);

    public void Reset() => Interlocked.Exchange(ref _readerExecutions, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
      DbCommand command,
      CommandEventData eventData,
      InterceptionResult<DbDataReader> result,
      CancellationToken cancellationToken = default)
    {
      Interlocked.Increment(ref _readerExecutions);
      return ValueTask.FromResult(result);
    }
  }

  private sealed class ResponseDisposer(IEnumerable<HttpResponseMessage> responses) : IDisposable
  {
    public void Dispose()
    {
      foreach (var response in responses)
      {
        response.Dispose();
      }
    }
  }
}
