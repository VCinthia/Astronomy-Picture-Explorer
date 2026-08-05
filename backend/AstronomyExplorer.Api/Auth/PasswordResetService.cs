using System.Data;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Auth;

/// <summary>
/// Keeps Identity's password update and refresh-session invalidation in the same
/// database transaction. A successful password reset therefore cannot leave an
/// active refresh session backed by the superseded password.
/// </summary>
public sealed class PasswordResetService(
  AppDbContext dbContext,
  UserManager<ApplicationUser> userManager,
  TimeProvider timeProvider,
  IUserSessionLock userSessionLock)
{
  public async Task<bool> ResetAsync(
    Guid userId,
    string token,
    string? password,
    CancellationToken cancellationToken)
  {
    await using var transaction = await dbContext.Database.BeginTransactionAsync(
      IsolationLevel.ReadCommitted,
      cancellationToken);
    await userSessionLock.AcquireAsync(userId, cancellationToken);
    var user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      await transaction.RollbackAsync(cancellationToken);
      return false;
    }

    var resetResult = await userManager.ResetPasswordAsync(user, token, password ?? string.Empty);
    if (!resetResult.Succeeded)
    {
      await transaction.RollbackAsync(cancellationToken);
      return false;
    }

    var now = timeProvider.GetUtcNow();
    await dbContext.RefreshSessions
      .Where(session => session.UserId == user.Id && session.RevokedAt == null)
      .ExecuteUpdateAsync(
        setters => setters.SetProperty(session => session.RevokedAt, now),
        cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return true;
  }
}
