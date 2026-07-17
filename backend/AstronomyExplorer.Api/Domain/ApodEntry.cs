using NpgsqlTypes;

namespace AstronomyExplorer.Api.Domain;

public sealed class ApodEntry
{
  public DateOnly Date { get; set; }

  public string Title { get; set; } = string.Empty;

  public string Explanation { get; set; } = string.Empty;

  public string MediaType { get; set; } = string.Empty;

  public string Url { get; set; } = string.Empty;

  public string? HdUrl { get; set; }

  public string? ThumbnailUrl { get; set; }

  public string? Copyright { get; set; }

  public NpgsqlTsVector SearchVector { get; private set; } = null!;

  public DateTimeOffset CachedAt { get; set; }

  public ICollection<Favorite> Favorites { get; } = [];
}
