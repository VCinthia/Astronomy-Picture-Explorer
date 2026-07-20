using System.Text.Json.Serialization;

namespace AstronomyExplorer.Api.Favorites;

public sealed record CreateFavoriteRequest(
  [property: JsonPropertyName("apod_date")] DateOnly? ApodDate);
