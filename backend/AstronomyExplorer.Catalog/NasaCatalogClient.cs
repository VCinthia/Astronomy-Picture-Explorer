using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AstronomyExplorer.Api.Apod;
using Microsoft.AspNetCore.WebUtilities;

namespace AstronomyExplorer.Catalog;

public interface INasaCatalogClient
{
  Task<IReadOnlyList<ApodEntryDto>> FetchRangeAsync(
    DateOnly from,
    DateOnly to,
    CancellationToken cancellationToken);
}

public interface ICatalogRetryDelay
{
  Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class CatalogRetryDelay : ICatalogRetryDelay
{
  public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
    Task.Delay(delay, cancellationToken);
}

public sealed class NasaCatalogClient(
  HttpClient httpClient,
  string apiKey,
  ICatalogRetryDelay retryDelay,
  TimeProvider timeProvider) : INasaCatalogClient
{
  private const int MaximumAttempts = 2;

  public async Task<IReadOnlyList<ApodEntryDto>> FetchRangeAsync(
    DateOnly from,
    DateOnly to,
    CancellationToken cancellationToken)
  {
    for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
    {
      try
      {
        using var request = CreateRequest(from, to);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
          NasaCatalogPayload?[]? payload;
          try
          {
            payload = await response.Content.ReadFromJsonAsync<NasaCatalogPayload?[]>(
              cancellationToken);
          }
          catch (Exception exception) when (
            exception is System.Text.Json.JsonException or NotSupportedException)
          {
            throw new CatalogNasaException(CatalogNasaFailure.InvalidPayload);
          }

          return ValidateAndMap(payload, from, to);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
          throw new CatalogNasaException(
            CatalogNasaFailure.RateLimited,
            ReadRetryNotBefore(response));
        }

        if (attempt < MaximumAttempts &&
            (response.StatusCode == HttpStatusCode.RequestTimeout ||
             response.StatusCode >= HttpStatusCode.InternalServerError))
        {
          await retryDelay.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
          continue;
        }

        throw new CatalogNasaException(
          response.StatusCode == HttpStatusCode.RequestTimeout ||
          response.StatusCode >= HttpStatusCode.InternalServerError
            ? CatalogNasaFailure.Transient
            : CatalogNasaFailure.Permanent);
      }
      catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        if (attempt == MaximumAttempts)
        {
          throw new CatalogNasaException(CatalogNasaFailure.Timeout);
        }

        await retryDelay.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
      }
      catch (HttpRequestException)
      {
        if (attempt == MaximumAttempts)
        {
          throw new CatalogNasaException(CatalogNasaFailure.Transient);
        }

        await retryDelay.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
      }
    }

    throw new CatalogNasaException(CatalogNasaFailure.Transient);
  }

  private HttpRequestMessage CreateRequest(DateOnly from, DateOnly to)
  {
    var path = QueryHelpers.AddQueryString(
      "planetary/apod",
      new Dictionary<string, string?>
      {
        ["start_date"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["end_date"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["thumbs"] = "true"
      });
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Add("X-Api-Key", apiKey);
    return request;
  }

  private static IReadOnlyList<ApodEntryDto> ValidateAndMap(
    NasaCatalogPayload?[]? payload,
    DateOnly from,
    DateOnly to)
  {
    if (payload is null || payload.Any(item => item is null))
    {
      throw new CatalogNasaException(CatalogNasaFailure.InvalidPayload);
    }

    var mapped = payload.Select(item => Map(item!)).OrderBy(entry => entry.Date).ToArray();
    var dates = new HashSet<DateOnly>();
    foreach (var entry in mapped)
    {
      if (entry.Date < from || entry.Date > to || !dates.Add(entry.Date))
      {
        throw new CatalogNasaException(CatalogNasaFailure.InvalidPayload);
      }
    }

    return mapped;
  }

  private static ApodEntryDto Map(NasaCatalogPayload payload)
  {
    if (!DateOnly.TryParseExact(
          payload.Date,
          "yyyy-MM-dd",
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out var date) ||
        string.IsNullOrWhiteSpace(payload.Title) ||
        string.IsNullOrWhiteSpace(payload.Explanation) ||
        payload.MediaType is not ("image" or "video") ||
        payload.ServiceVersion != "v1" ||
        !IsAbsoluteHttpUrl(payload.Url))
    {
      throw new CatalogNasaException(CatalogNasaFailure.InvalidPayload);
    }

    return new ApodEntryDto(
      date,
      payload.Title.Trim(),
      payload.Explanation.Trim(),
      payload.MediaType,
      payload.Url!.Trim(),
      NormalizeOptionalUrl(payload.HdUrl),
      NormalizeOptionalUrl(payload.ThumbnailUrl),
      NormalizeOptional(payload.Copyright));
  }

  private static bool IsAbsoluteHttpUrl(string? value) =>
    Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

  private static string? NormalizeOptionalUrl(string? value)
  {
    var normalized = NormalizeOptional(value);
    if (normalized is not null && !IsAbsoluteHttpUrl(normalized))
    {
      throw new CatalogNasaException(CatalogNasaFailure.InvalidPayload);
    }

    return normalized;
  }

  private static string? NormalizeOptional(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private DateTimeOffset? ReadRetryNotBefore(HttpResponseMessage response)
  {
    var now = timeProvider.GetUtcNow();
    var retryAfter = response.Headers.RetryAfter;
    if (retryAfter?.Delta is { } delta)
    {
      return now + (delta < TimeSpan.Zero ? TimeSpan.Zero : delta);
    }

    if (retryAfter?.Date is { } date)
    {
      return date < now ? now : date;
    }

    return now.AddHours(1);
  }

  private sealed record NasaCatalogPayload(
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("media_type")] string? MediaType,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("hdurl")] string? HdUrl,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl,
    [property: JsonPropertyName("copyright")] string? Copyright,
    [property: JsonPropertyName("service_version")] string? ServiceVersion);
}

public enum CatalogNasaFailure
{
  RateLimited,
  Timeout,
  Transient,
  Permanent,
  InvalidPayload
}

public sealed class CatalogNasaException(
  CatalogNasaFailure failure,
  DateTimeOffset? retryNotBefore = null)
  : Exception("The APOD catalog provider request failed.")
{
  public CatalogNasaFailure Failure { get; } = failure;

  public DateTimeOffset? RetryNotBefore { get; } = retryNotBefore;
}
