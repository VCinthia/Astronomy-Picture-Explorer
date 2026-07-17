using System.Collections.Concurrent;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Nasa;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Apod;

public sealed class ApodCacheOptions
{
  public const string SectionName = "ApodCache";

  public TimeSpan MemoryLifetime { get; init; } = TimeSpan.FromHours(6);

  public int MaxEntries { get; init; } = 512;
}

public sealed class ApodCacheOptionsValidator : IValidateOptions<ApodCacheOptions>
{
  public ValidateOptionsResult Validate(string? name, ApodCacheOptions options)
  {
    var failures = new List<string>();
    if (options.MemoryLifetime <= TimeSpan.Zero ||
        options.MemoryLifetime > TimeSpan.FromDays(1))
    {
      failures.Add("ApodCache:MemoryLifetime must be between zero and one day.");
    }

    if (options.MaxEntries is < 1 or > 10_000)
    {
      failures.Add("ApodCache:MaxEntries must be between 1 and 10000.");
    }

    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }
}

public sealed class ApodSingleFlight
{
  private readonly ConcurrentDictionary<DateOnly, Lazy<Task<ApodEntryDto>>> _operations = new();

  public Task<ApodEntryDto> RunAsync(
    DateOnly date,
    Func<Task<ApodEntryDto>> operation,
    CancellationToken cancellationToken)
  {
    var lazy = _operations.GetOrAdd(
      date,
      _ => new Lazy<Task<ApodEntryDto>>(
        () => RunAndRemoveAsync(date, operation),
        LazyThreadSafetyMode.ExecutionAndPublication));
    return lazy.Value.WaitAsync(cancellationToken);
  }

  private async Task<ApodEntryDto> RunAndRemoveAsync(
    DateOnly date,
    Func<Task<ApodEntryDto>> operation)
  {
    try
    {
      return await operation();
    }
    finally
    {
      _operations.TryRemove(date, out _);
    }
  }
}

public sealed class ApodCacheService(
  IMemoryCache memoryCache,
  ApodSingleFlight singleFlight,
  IServiceScopeFactory scopeFactory,
  IOptions<ApodCacheOptions> options,
  IOptions<NasaApodOptions> nasaOptions,
  IHostApplicationLifetime applicationLifetime)
{
  private readonly ApodCacheOptions _options = options.Value;
  private readonly TimeSpan _operationTimeout =
    (nasaOptions.Value.Timeout * nasaOptions.Value.MaxAttempts) + TimeSpan.FromSeconds(5);

  public async Task<ApodEntryDto> GetAsync(
    DateOnly date,
    CancellationToken cancellationToken)
  {
    if (memoryCache.TryGetValue(CacheKey(date), out ApodEntryDto? cached) && cached is not null)
    {
      return cached;
    }

    return await singleFlight.RunAsync(
      date,
      () => LoadAndCacheInScopeAsync(date),
      cancellationToken);
  }

  private async Task<ApodEntryDto> LoadAndCacheInScopeAsync(DateOnly date)
  {
    using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
      applicationLifetime.ApplicationStopping);
    operationCancellation.CancelAfter(_operationTimeout);
    var cancellationToken = operationCancellation.Token;

    try
    {
      if (memoryCache.TryGetValue(CacheKey(date), out ApodEntryDto? cached) && cached is not null)
      {
        return cached;
      }

      await using var scope = scopeFactory.CreateAsyncScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var nasaClient = scope.ServiceProvider.GetRequiredService<INasaApodClient>();
      var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
      var persisted = await dbContext.ApodEntries
        .AsNoTracking()
        .SingleOrDefaultAsync(entry => entry.Date == date, cancellationToken);
      if (persisted is not null)
      {
        return StoreInMemory(Map(persisted));
      }

      var fetched = await nasaClient.GetByDateAsync(date, cancellationToken);
      var cachedAt = timeProvider.GetUtcNow();
      await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
        INSERT INTO apod_entries
          (date, title, explanation, media_type, url, hdurl, thumbnail_url, copyright, cached_at)
        VALUES
          ({{fetched.Date}}, {{fetched.Title}}, {{fetched.Explanation}}, {{fetched.MediaType}},
           {{fetched.Url}}, {{fetched.HdUrl}}, {{fetched.ThumbnailUrl}}, {{fetched.Copyright}}, {{cachedAt}})
        ON CONFLICT (date) DO UPDATE SET
          title = EXCLUDED.title,
          explanation = EXCLUDED.explanation,
          media_type = EXCLUDED.media_type,
          url = EXCLUDED.url,
          hdurl = EXCLUDED.hdurl,
          thumbnail_url = EXCLUDED.thumbnail_url,
          copyright = EXCLUDED.copyright,
          cached_at = EXCLUDED.cached_at
        """, cancellationToken);
      return StoreInMemory(fetched);
    }
    catch (OperationCanceledException) when (
      !applicationLifetime.ApplicationStopping.IsCancellationRequested)
    {
      throw new NasaApodException(NasaApodFailure.Timeout);
    }
  }

  private ApodEntryDto StoreInMemory(ApodEntryDto entry)
  {
    memoryCache.Set(
      CacheKey(entry.Date),
      entry,
      new MemoryCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = _options.MemoryLifetime,
        Size = 1
      });
    return entry;
  }

  private static ApodEntryDto Map(ApodEntry entry) => new(
    entry.Date,
    entry.Title,
    entry.Explanation,
    entry.MediaType,
    entry.Url,
    entry.HdUrl,
    entry.ThumbnailUrl,
    entry.Copyright);

  private static string CacheKey(DateOnly date) => $"apod:{date:yyyy-MM-dd}";
}
