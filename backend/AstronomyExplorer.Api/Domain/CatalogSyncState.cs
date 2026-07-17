namespace AstronomyExplorer.Api.Domain;

public sealed class CatalogSyncState
{
  public Guid Id { get; set; }

  public DateOnly TargetFrom { get; set; }

  public DateOnly TargetTo { get; set; }

  public DateOnly? LastCompletedDate { get; set; }

  public CatalogSyncStatus Status { get; set; }

  public string? LastError { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }
}
