using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Catalog;

public sealed record CatalogSyncResult(
  CatalogSyncStatus Status,
  DateOnly? LastCompletedDate,
  int CompletedBatches,
  bool WasAlreadyCompleted);

public sealed class CatalogSyncService(
  string connectionString,
  Func<AppDbContext> createDbContext,
  INasaCatalogClient nasaClient,
  TimeProvider timeProvider,
  TimeSpan? lockHeartbeatInterval = null)
{
  public async Task<CatalogSyncResult> SynchronizeAsync(
    CatalogSyncCommand command,
    CancellationToken cancellationToken)
  {
    await using var globalLock = await CatalogGlobalLock.AcquireAsync(
      connectionString,
      lockHeartbeatInterval ?? TimeSpan.FromSeconds(5),
      cancellationToken);
    using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
      cancellationToken,
      globalLock.LockLostToken);
    var operationToken = operationCancellation.Token;
    try
    {
      var state = await StartOrResumeAsync(command, operationToken);
      if (state.Status == CatalogSyncStatus.Completed &&
          state.LastCompletedDate == state.TargetTo)
      {
        return new CatalogSyncResult(
          state.Status,
          state.LastCompletedDate,
          0,
          WasAlreadyCompleted: true);
      }

      var completedBatches = 0;
      var nextDate = state.LastCompletedDate?.AddDays(1) ?? command.From;
      while (nextDate <= command.To)
      {
        operationToken.ThrowIfCancellationRequested();
        var batchTo = nextDate.AddDays(command.BatchSize - 1);
        if (batchTo > command.To)
        {
          batchTo = command.To;
        }

        var entries = await nasaClient.FetchRangeAsync(nextDate, batchTo, operationToken);
        await PersistBatchAndCheckpointAsync(
          command,
          entries,
          batchTo,
          operationToken);
        completedBatches++;
        nextDate = batchTo.AddDays(1);
      }

      return new CatalogSyncResult(
        CatalogSyncStatus.Completed,
        command.To,
        completedBatches,
        WasAlreadyCompleted: false);
    }
    catch (CatalogNasaException exception)
    {
      var status = exception.Failure is
        CatalogNasaFailure.RateLimited or
        CatalogNasaFailure.Timeout or
        CatalogNasaFailure.Transient
        ? CatalogSyncStatus.Paused
        : CatalogSyncStatus.Failed;
      await UpdateFailureBestEffortAsync(
        command,
        status,
        FormatProviderFailure(exception),
        exception.RetryNotBefore);
      throw;
    }
    catch (CatalogUsageException)
    {
      throw;
    }
    catch (CatalogSafetyException)
    {
      throw;
    }
    catch (OperationCanceledException) when (globalLock.LockLostToken.IsCancellationRequested)
    {
      await UpdateFailureBestEffortAsync(
        command,
        CatalogSyncStatus.Paused,
        "Catalog lock connection was lost; resume is safe.");
      throw new CatalogConcurrencyException(
        "Catalog synchronization stopped because its database lock was lost.");
    }
    catch (OperationCanceledException)
    {
      await UpdateFailureBestEffortAsync(
        command,
        CatalogSyncStatus.Paused,
        "Synchronization interrupted; resume is safe.");
      throw;
    }
    catch
    {
      await UpdateFailureBestEffortAsync(
        command,
        CatalogSyncStatus.Failed,
        "Catalog persistence failed; checkpoint was not advanced.");
      throw;
    }
  }

  private async Task<CatalogSyncState> StartOrResumeAsync(
    CatalogSyncCommand command,
    CancellationToken cancellationToken)
  {
    await using var context = createDbContext();
    var state = await context.CatalogSyncStates.SingleOrDefaultAsync(
      item => item.TargetFrom == command.From && item.TargetTo == command.To,
      cancellationToken);
    if (state is not null)
    {
      if (state.Status == CatalogSyncStatus.Completed &&
          state.LastCompletedDate == state.TargetTo)
      {
        var persistedCount = await context.ApodEntries.LongCountAsync(
          entry => entry.Date >= command.From && entry.Date <= command.To,
          cancellationToken);
        if (persistedCount >= state.SyncedEntryCount)
        {
          return state;
        }

        if (!command.Resume)
        {
          throw new CatalogUsageException(
            "Completed catalog data has drifted; use --resume to repair the full range.");
        }

        state.LastCompletedDate = null;
        state.SyncedEntryCount = 0;
      }
      else if (!command.Resume)
      {
        throw new CatalogUsageException(
          "An incomplete synchronization exists for this range; use --resume.");
      }

      state.Status = CatalogSyncStatus.Running;
      if (state.RetryNotBefore is { } retryNotBefore &&
          retryNotBefore > timeProvider.GetUtcNow())
      {
        throw new CatalogSafetyException(
          $"Catalog resume is paused until {retryNotBefore:O}.");
      }

      state.LastError = null;
      state.RetryNotBefore = null;
      state.UpdatedAt = timeProvider.GetUtcNow();
    }
    else
    {
      var now = timeProvider.GetUtcNow();
      state = new CatalogSyncState
      {
        Id = Guid.NewGuid(),
        TargetFrom = command.From,
        TargetTo = command.To,
        Status = CatalogSyncStatus.Running,
        CreatedAt = now,
        UpdatedAt = now
      };
      context.CatalogSyncStates.Add(state);
    }

    await context.SaveChangesAsync(cancellationToken);
    return state;
  }

  private async Task PersistBatchAndCheckpointAsync(
    CatalogSyncCommand command,
    IReadOnlyList<ApodEntryDto> entries,
    DateOnly batchTo,
    CancellationToken cancellationToken)
  {
    await using var context = createDbContext();
    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    var cachedAt = timeProvider.GetUtcNow();
    foreach (var entry in entries)
    {
      await context.Database.ExecuteSqlInterpolatedAsync($$"""
        INSERT INTO apod_entries
          (date, title, explanation, media_type, url, hdurl, thumbnail_url, copyright, cached_at)
        VALUES
          ({{entry.Date}}, {{entry.Title}}, {{entry.Explanation}}, {{entry.MediaType}},
           {{entry.Url}}, {{entry.HdUrl}}, {{entry.ThumbnailUrl}}, {{entry.Copyright}}, {{cachedAt}})
        ON CONFLICT (date) DO UPDATE SET
          title = EXCLUDED.title,
          explanation = EXCLUDED.explanation,
          media_type = EXCLUDED.media_type,
          url = EXCLUDED.url,
          hdurl = EXCLUDED.hdurl,
          thumbnail_url = EXCLUDED.thumbnail_url,
          copyright = EXCLUDED.copyright,
          cached_at = EXCLUDED.cached_at
        """, cancellationToken);
    }

    var state = await context.CatalogSyncStates.SingleAsync(
      item => item.TargetFrom == command.From && item.TargetTo == command.To,
      cancellationToken);
    state.LastCompletedDate = batchTo;
    state.SyncedEntryCount += entries.Count;
    state.Status = batchTo == command.To
      ? CatalogSyncStatus.Completed
      : CatalogSyncStatus.Running;
    state.LastError = null;
    state.RetryNotBefore = null;
    state.UpdatedAt = cachedAt;
    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  private async Task UpdateFailureBestEffortAsync(
    CatalogSyncCommand command,
    CatalogSyncStatus status,
    string error,
    DateTimeOffset? retryNotBefore = null)
  {
    try
    {
      await using var context = createDbContext();
      var state = await context.CatalogSyncStates.SingleOrDefaultAsync(
        item => item.TargetFrom == command.From && item.TargetTo == command.To);
      if (state is null || state.Status == CatalogSyncStatus.Completed)
      {
        return;
      }

      state.Status = status;
      state.LastError = error.Length <= 256 ? error : error[..256];
      state.RetryNotBefore = retryNotBefore;
      state.UpdatedAt = timeProvider.GetUtcNow();
      await context.SaveChangesAsync();
    }
    catch
    {
      // Best effort only: preserve the original sanitized failure.
    }
  }

  private static string FormatProviderFailure(CatalogNasaException exception)
  {
    if (exception.Failure != CatalogNasaFailure.RateLimited)
    {
      return "APOD provider request failed; checkpoint was not advanced.";
    }

    return exception.RetryNotBefore is { } retryNotBefore
      ? $"NASA rate limit reached; resume no earlier than {retryNotBefore:O}."
      : "NASA rate limit reached; resume after the provider window resets.";
  }
}
