using System.Text.Json.Serialization;

namespace AstronomyExplorer.Api.Apod;

public sealed record ApodEntryDto(
  [property: JsonPropertyName("date")] DateOnly Date,
  [property: JsonPropertyName("title")] string Title,
  [property: JsonPropertyName("explanation")] string Explanation,
  [property: JsonPropertyName("media_type")] string MediaType,
  [property: JsonPropertyName("url")] string Url,
  [property: JsonPropertyName("hdurl")] string? HdUrl,
  [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl,
  [property: JsonPropertyName("copyright")] string? Copyright);
