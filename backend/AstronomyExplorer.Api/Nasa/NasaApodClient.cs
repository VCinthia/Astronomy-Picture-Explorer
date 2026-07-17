using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AstronomyExplorer.Api.Apod;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Nasa;

public interface INasaApodClient
{
  Task<ApodEntryDto> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);
}

public sealed class NasaApodClient(
  HttpClient httpClient,
  IOptions<NasaApodOptions> options) : INasaApodClient
{
  private readonly NasaApodOptions _options = options.Value;

  public async Task<ApodEntryDto> GetByDateAsync(
    DateOnly date,
    CancellationToken cancellationToken)
  {
    for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
    {
      try
      {
        using var request = CreateRequest(date);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
          NasaApodResponse? payload;
          try
          {
            payload = await response.Content.ReadFromJsonAsync<NasaApodResponse>(
              cancellationToken);
          }
          catch (Exception exception) when (
            exception is System.Text.Json.JsonException or NotSupportedException)
          {
            throw new NasaApodException(NasaApodFailure.InvalidPayload);
          }

          return Map(payload, date);
        }

        var failure = response.StatusCode == HttpStatusCode.TooManyRequests
          ? NasaApodFailure.RateLimited
          : NasaApodFailure.Upstream;
        if (attempt < _options.MaxAttempts &&
            response.StatusCode >= HttpStatusCode.InternalServerError)
        {
          continue;
        }

        throw new NasaApodException(failure);
      }
      catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        if (attempt == _options.MaxAttempts)
        {
          throw new NasaApodException(NasaApodFailure.Timeout);
        }
      }
      catch (HttpRequestException)
      {
        if (attempt == _options.MaxAttempts)
        {
          throw new NasaApodException(NasaApodFailure.Upstream);
        }
      }
    }

    throw new NasaApodException(NasaApodFailure.Upstream);
  }

  private HttpRequestMessage CreateRequest(DateOnly date)
  {
    var path = QueryHelpers.AddQueryString(
      "planetary/apod",
      new Dictionary<string, string?>
      {
        ["date"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["thumbs"] = "true"
      });
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Add("X-Api-Key", _options.ApiKey);
    return request;
  }

  private static ApodEntryDto Map(NasaApodResponse? payload, DateOnly requestedDate)
  {
    if (payload is null ||
        !DateOnly.TryParseExact(
          payload.Date,
          "yyyy-MM-dd",
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out var responseDate) ||
        responseDate != requestedDate ||
        string.IsNullOrWhiteSpace(payload.Title) ||
        string.IsNullOrWhiteSpace(payload.Explanation) ||
        payload.ServiceVersion != "v1" ||
        !IsAbsoluteHttpUrl(payload.Url) ||
        payload.MediaType is not ("image" or "video"))
    {
      throw new NasaApodException(NasaApodFailure.InvalidPayload);
    }

    return new ApodEntryDto(
      responseDate,
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
    if (normalized is null)
    {
      return null;
    }

    if (!IsAbsoluteHttpUrl(normalized))
    {
      throw new NasaApodException(NasaApodFailure.InvalidPayload);
    }

    return normalized;
  }

  private static string? NormalizeOptional(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private sealed record NasaApodResponse(
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

public enum NasaApodFailure
{
  RateLimited,
  Timeout,
  Upstream,
  InvalidPayload
}

public sealed class NasaApodException(NasaApodFailure failure)
  : Exception("The APOD provider request failed.")
{
  public NasaApodFailure Failure { get; } = failure;
}

public static class NasaApodHttpClientConfiguration
{
  public static HttpClientHandler CreatePrimaryHandler() => new()
  {
    AllowAutoRedirect = false
  };
}
