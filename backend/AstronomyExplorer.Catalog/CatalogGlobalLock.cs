using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace AstronomyExplorer.Catalog;

public sealed class CatalogGlobalLock : IAsyncDisposable
{
  private readonly NpgsqlConnection _connection;
  private readonly long _lockKey;
  private readonly CancellationTokenSource _heartbeatCancellation = new();
  private readonly CancellationTokenSource _lockLostCancellation = new();
  private readonly Task _heartbeatTask;

  private CatalogGlobalLock(
    NpgsqlConnection connection,
    long lockKey,
    TimeSpan heartbeatInterval)
  {
    _connection = connection;
    _lockKey = lockKey;
    _heartbeatTask = MonitorAsync(heartbeatInterval);
  }

  public CancellationToken LockLostToken => _lockLostCancellation.Token;

  public static async Task<CatalogGlobalLock> AcquireAsync(
    string connectionString,
    TimeSpan heartbeatInterval,
    CancellationToken cancellationToken)
  {
    if (heartbeatInterval <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
    }

    var connection = new NpgsqlConnection(connectionString);
    try
    {
      await connection.OpenAsync(cancellationToken);
      var lockKey = CreateLockKey();

      await using var command = new NpgsqlCommand(
        "SELECT pg_try_advisory_lock($1);",
        connection);
      command.Parameters.AddWithValue(lockKey);
      var acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
      if (!acquired)
      {
        throw new CatalogConcurrencyException(
          "Another catalog synchronization is already running.");
      }

      return new CatalogGlobalLock(connection, lockKey, heartbeatInterval);
    }
    catch
    {
      await connection.DisposeAsync();
      throw;
    }
  }

  public async ValueTask DisposeAsync()
  {
    _heartbeatCancellation.Cancel();
    try
    {
      await _heartbeatTask;
    }
    catch (OperationCanceledException)
    {
    }

    try
    {
      if (!_lockLostCancellation.IsCancellationRequested &&
          _connection.FullState.HasFlag(System.Data.ConnectionState.Open))
      {
        await using var command = new NpgsqlCommand(
          "SELECT pg_advisory_unlock($1);",
          _connection);
        command.Parameters.AddWithValue(_lockKey);
        await command.ExecuteScalarAsync();
      }
    }
    catch (NpgsqlException)
    {
    }
    finally
    {
      await _connection.DisposeAsync();
      _heartbeatCancellation.Dispose();
      _lockLostCancellation.Dispose();
    }
  }

  private async Task MonitorAsync(TimeSpan heartbeatInterval)
  {
    try
    {
      while (true)
      {
        await Task.Delay(heartbeatInterval, _heartbeatCancellation.Token);
        await using var command = new NpgsqlCommand("SELECT 1;", _connection)
        {
          CommandTimeout = 5
        };
        await command.ExecuteScalarAsync(_heartbeatCancellation.Token);
      }
    }
    catch (OperationCanceledException) when (_heartbeatCancellation.IsCancellationRequested)
    {
    }
    catch
    {
      _lockLostCancellation.Cancel();
    }
  }

  private static long CreateLockKey()
  {
    var value = Encoding.UTF8.GetBytes("astronomy-catalog:global");
    var hash = SHA256.HashData(value);
    return BinaryPrimitives.ReadInt64LittleEndian(hash);
  }
}

public sealed class CatalogConcurrencyException(string message) : Exception(message);
