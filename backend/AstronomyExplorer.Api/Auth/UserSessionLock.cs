using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AstronomyExplorer.Api.Auth;

/// <summary>
/// Serializes password changes with session creation for one user. It closes the
/// login/reset race in which a password verified just before a reset could otherwise
/// insert a fresh refresh session after the reset's bulk revocation.
/// </summary>
public interface IUserSessionLock
{
  Task AcquireAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class UserSessionLock(AppDbContext dbContext) : IUserSessionLock
{
  public async Task AcquireAsync(Guid userId, CancellationToken cancellationToken)
  {
    var userBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"user:{userId:N}"));
    var advisoryKey = BinaryPrimitives.ReadInt64BigEndian(userBytes.AsSpan(0, sizeof(long)));
    await using var command = dbContext.Database.GetDbConnection().CreateCommand();
    command.Transaction = dbContext.Database.CurrentTransaction!.GetDbTransaction();
    command.CommandText = "SELECT pg_advisory_xact_lock(@user_key)";
    command.Parameters.Add(new NpgsqlParameter<long>("user_key", advisoryKey));
    await command.ExecuteNonQueryAsync(cancellationToken);
  }
}
