using System.Data;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AstronomyExplorer.Api.Auth;

public sealed class RefreshSessionService(
  AppDbContext dbContext,
  IOptions<AuthSessionOptions> options,
  TimeProvider timeProvider,
  IUserSessionLock userSessionLock)
{
  private readonly AuthSessionOptions _options = options.Value;

  public async Task<CreatedRefreshSession?> CreateAsync(
    ApplicationUser user,
    CancellationToken cancellationToken)
  {
    await using var transaction = await dbContext.Database.BeginTransactionAsync(
      IsolationLevel.ReadCommitted,
      cancellationToken);
    await userSessionLock.AcquireAsync(user.Id, cancellationToken);
    var securityStampMatches = await dbContext.Users
      .AsNoTracking()
      .AnyAsync(
        candidate => candidate.Id == user.Id && candidate.SecurityStamp == user.SecurityStamp,
        cancellationToken);
    if (!securityStampMatches)
    {
      await transaction.RollbackAsync(cancellationToken);
      return null;
    }

    var token = CreateRawToken();
    var now = timeProvider.GetUtcNow();
    var session = new RefreshSession
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      TokenHash = Hash(token),
      FamilyId = Guid.NewGuid(),
      CreatedAt = now,
      ExpiresAt = now.Add(_options.RefreshTokenLifetime)
    };
    dbContext.RefreshSessions.Add(session);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return new CreatedRefreshSession(token, session.ExpiresAt);
  }

  public async Task<RotateRefreshResult> RotateAsync(
    string rawToken,
    CancellationToken cancellationToken)
  {
    var tokenHash = Hash(rawToken);
    await using var transaction = await dbContext.Database.BeginTransactionAsync(
      IsolationLevel.ReadCommitted,
      cancellationToken);
    var familyId = await dbContext.RefreshSessions
      .AsNoTracking()
      .Where(candidate => candidate.TokenHash == tokenHash)
      .Select(candidate => new RefreshSessionIdentity(candidate.UserId, candidate.FamilyId))
      .SingleOrDefaultAsync(cancellationToken);
    if (familyId is null)
    {
      await transaction.RollbackAsync(cancellationToken);
      return RotateRefreshResult.Invalid;
    }

    await userSessionLock.AcquireAsync(familyId.UserId, cancellationToken);
    var lockedIdentity = await dbContext.RefreshSessions
      .AsNoTracking()
      .Where(candidate => candidate.TokenHash == tokenHash)
      .Select(candidate => new RefreshSessionIdentity(candidate.UserId, candidate.FamilyId))
      .SingleOrDefaultAsync(cancellationToken);
    if (lockedIdentity is null || lockedIdentity.UserId != familyId.UserId)
    {
      await transaction.RollbackAsync(cancellationToken);
      return RotateRefreshResult.Invalid;
    }

    await AcquireFamilyLockAsync(lockedIdentity.FamilyId, cancellationToken);
    var session = await dbContext.RefreshSessions
      .Include(candidate => candidate.User)
      .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
    if (session is null)
    {
      await transaction.RollbackAsync(cancellationToken);
      return RotateRefreshResult.Invalid;
    }

    var now = timeProvider.GetUtcNow();
    if (session.RevokedAt is not null)
    {
      await dbContext.RefreshSessions
        .Where(candidate => candidate.FamilyId == session.FamilyId && candidate.RevokedAt == null)
        .ExecuteUpdateAsync(
          setters => setters.SetProperty(candidate => candidate.RevokedAt, now),
          cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      return RotateRefreshResult.Invalid;
    }

    if (session.ExpiresAt <= now)
    {
      session.RevokedAt = now;
      await dbContext.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      return RotateRefreshResult.Invalid;
    }

    var replacementToken = CreateRawToken();
    var replacement = new RefreshSession
    {
      Id = Guid.NewGuid(),
      UserId = session.UserId,
      TokenHash = Hash(replacementToken),
      FamilyId = session.FamilyId,
      CreatedAt = now,
      ExpiresAt = now.Add(_options.RefreshTokenLifetime)
    };
    session.RevokedAt = now;
    session.ReplacedByTokenId = replacement.Id;
    dbContext.RefreshSessions.Add(replacement);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return new RotateRefreshResult(
      true,
      replacementToken,
      replacement.ExpiresAt,
      session.User);
  }

  public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
  {
    var tokenHash = Hash(rawToken);
    await using var transaction = await dbContext.Database.BeginTransactionAsync(
      IsolationLevel.ReadCommitted,
      cancellationToken);
    var familyId = await dbContext.RefreshSessions
      .AsNoTracking()
      .Where(candidate => candidate.TokenHash == tokenHash)
      .Select(candidate => (Guid?)candidate.FamilyId)
      .SingleOrDefaultAsync(cancellationToken);
    if (familyId is null)
    {
      await transaction.CommitAsync(cancellationToken);
      return;
    }

    await AcquireFamilyLockAsync(familyId.Value, cancellationToken);
    var lockedFamilyId = await dbContext.RefreshSessions
      .AsNoTracking()
      .Where(candidate => candidate.TokenHash == tokenHash)
      .Select(candidate => (Guid?)candidate.FamilyId)
      .SingleOrDefaultAsync(cancellationToken);
    if (lockedFamilyId != familyId)
    {
      await transaction.CommitAsync(cancellationToken);
      return;
    }

    var now = timeProvider.GetUtcNow();
    await dbContext.RefreshSessions
      .Where(candidate => candidate.FamilyId == familyId.Value && candidate.RevokedAt == null)
      .ExecuteUpdateAsync(
        setters => setters.SetProperty(candidate => candidate.RevokedAt, now),
        cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  public static string Hash(string rawToken) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

  private static string CreateRawToken()
  {
    Span<byte> bytes = stackalloc byte[32];
    RandomNumberGenerator.Fill(bytes);
    return WebEncoders.Base64UrlEncode(bytes);
  }

  private async Task AcquireFamilyLockAsync(
    Guid familyId,
    CancellationToken cancellationToken)
  {
    var familyBytes = SHA256.HashData(familyId.ToByteArray());
    var advisoryKey = BinaryPrimitives.ReadInt64BigEndian(familyBytes.AsSpan(0, sizeof(long)));
    await using var command = dbContext.Database.GetDbConnection().CreateCommand();
    command.Transaction = dbContext.Database.CurrentTransaction!.GetDbTransaction();
    command.CommandText = "SELECT pg_advisory_xact_lock(@family_key)";
    command.Parameters.Add(new NpgsqlParameter<long>("family_key", advisoryKey));
    await command.ExecuteNonQueryAsync(cancellationToken);
  }
}

public sealed record CreatedRefreshSession(string RawToken, DateTimeOffset ExpiresAt);

internal sealed record RefreshSessionIdentity(Guid UserId, Guid FamilyId);

public sealed record RotateRefreshResult(
  bool Succeeded,
  string? RawToken,
  DateTimeOffset ExpiresAt,
  ApplicationUser? User)
{
  public static RotateRefreshResult Invalid { get; } = new(false, null, default, null);
}
