namespace AstronomyExplorer.Api.Domain;

public sealed class Favorite
{
  public Guid UserId { get; set; }

  public DateOnly ApodDate { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public ApplicationUser User { get; set; } = null!;

  public ApodEntry ApodEntry { get; set; } = null!;
}
