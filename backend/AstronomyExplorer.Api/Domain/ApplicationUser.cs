using Microsoft.AspNetCore.Identity;

namespace AstronomyExplorer.Api.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
  public ICollection<RefreshSession> RefreshSessions { get; } = [];

  public ICollection<Favorite> Favorites { get; } = [];
}
