namespace AstronomyExplorer.Api.Domain;

public sealed class RefreshSession
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public string TokenHash { get; set; } = string.Empty;

  public Guid FamilyId { get; set; }

  public Guid? ReplacedByTokenId { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset ExpiresAt { get; set; }

  public DateTimeOffset? RevokedAt { get; set; }

  public ApplicationUser User { get; set; } = null!;

  public RefreshSession? ReplacedByToken { get; set; }
}
