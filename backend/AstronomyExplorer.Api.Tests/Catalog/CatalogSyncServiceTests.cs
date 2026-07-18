using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using AstronomyExplorer.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AstronomyExplorer.Api.Tests.Catalog;

[Collection(PostgreSqlCollection.Name)]
public sealed class CatalogSyncServiceTests(PostgreSqlFixture database)
{
  private static readonly TimeProvider Clock = new FixedTimeProvider(
    new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero));

  [Fact]
  public async Task CatalogSyncState_NegativeSyncedCountIsRejected()
  {
    await PostgresAssert.ThrowsSqlStateAsync(
      database.ConnectionString,
      """
      INSERT INTO catalog_sync_state
        (id, target_from, target_to, synced_entry_count, status, created_at, updated_at)
      VALUES
        (gen_random_uuid(), DATE '2014-01-01', DATE '2014-01-02', -1, 'Pending', NOW(), NOW());
      """,
      PostgresErrorCodes.CheckViolation);
  }

  [Fact]
  public async Task Synchronize_BatchesPersistEntriesAndCheckpointAtomically()
  {
    var command = Command(new DateOnly(2012, 1, 1), new DateOnly(2012, 1, 3), 2);
    await CleanRangeAsync(command);
    var provider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));

    var result = await CreateService(provider).SynchronizeAsync(command, CancellationToken.None);

    Assert.Equal(CatalogSyncStatus.Completed, result.Status);
    Assert.Equal(2, result.CompletedBatches);
    Assert.Equal(
      [(new DateOnly(2012, 1, 1), new DateOnly(2012, 1, 2)),
       (new DateOnly(2012, 1, 3), new DateOnly(2012, 1, 3))],
      provider.Requests);
    await using var context = database.CreateDbContext();
    Assert.Equal(3, await context.ApodEntries.CountAsync(
      entry => entry.Date >= command.From && entry.Date <= command.To));
    var state = await FindStateAsync(context, command);
    Assert.Equal(command.To, state.LastCompletedDate);
    Assert.Equal(3, state.SyncedEntryCount);
    Assert.Equal(CatalogSyncStatus.Completed, state.Status);
  }

  [Fact]
  public async Task Synchronize_SparseBatchAdvancesRangeAndCountsOnlyReturnedEntries()
  {
    var command = Command(new DateOnly(1995, 6, 16), new DateOnly(1995, 6, 20), 5);
    await CleanRangeAsync(command);
    var provider = new FakeCatalogClient((_, _, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(
        [Entry(command.From), Entry(command.To)]));

    await CreateService(provider).SynchronizeAsync(command, CancellationToken.None);

    await using var context = database.CreateDbContext();
    var state = await FindStateAsync(context, command);
    Assert.Equal(command.To, state.LastCompletedDate);
    Assert.Equal(2, state.SyncedEntryCount);
    Assert.Equal(2, await context.ApodEntries.CountAsync(
      entry => entry.Date >= command.From && entry.Date <= command.To));
  }

  [Fact]
  public async Task Synchronize_FailedBatchRollsBackEntriesAndCheckpoint()
  {
    var command = Command(new DateOnly(2012, 2, 1), new DateOnly(2012, 2, 2), 2);
    await CleanRangeAsync(command);
    var invalidEntries = Entries(command.From, command.To).ToArray();
    invalidEntries[1] = invalidEntries[1] with { MediaType = "audio" };
    var provider = new FakeCatalogClient((_, _, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(invalidEntries));

    await Assert.ThrowsAnyAsync<Exception>(() =>
      CreateService(provider).SynchronizeAsync(command, CancellationToken.None));

    await using var context = database.CreateDbContext();
    Assert.Equal(0, await context.ApodEntries.CountAsync(
      entry => entry.Date >= command.From && entry.Date <= command.To));
    var state = await FindStateAsync(context, command);
    Assert.Null(state.LastCompletedDate);
    Assert.Equal(CatalogSyncStatus.Failed, state.Status);
  }

  [Fact]
  public async Task Synchronize_InterruptedThenResumeMatchesContinuousResult()
  {
    var command = Command(new DateOnly(2012, 3, 1), new DateOnly(2012, 3, 4), 2);
    await CleanRangeAsync(command);
    var interruptedProvider = new FakeCatalogClient((from, to, _) =>
      from == command.From
        ? Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to))
        : Task.FromException<IReadOnlyList<ApodEntryDto>>(new OperationCanceledException()));

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      CreateService(interruptedProvider).SynchronizeAsync(command, CancellationToken.None));

    await using (var interruptedContext = database.CreateDbContext())
    {
      var paused = await FindStateAsync(interruptedContext, command);
      Assert.Equal(new DateOnly(2012, 3, 2), paused.LastCompletedDate);
      Assert.Equal(2, paused.SyncedEntryCount);
      Assert.Equal(CatalogSyncStatus.Paused, paused.Status);
    }

    var resumeProvider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));
    var resumed = command with { Resume = true };
    var result = await CreateService(resumeProvider).SynchronizeAsync(
      resumed,
      CancellationToken.None);

    Assert.Equal(
      [(new DateOnly(2012, 3, 3), new DateOnly(2012, 3, 4))],
      resumeProvider.Requests);
    Assert.Equal(CatalogSyncStatus.Completed, result.Status);
    await using var context = database.CreateDbContext();
    Assert.Equal(4, await context.ApodEntries.CountAsync(
      entry => entry.Date >= command.From && entry.Date <= command.To));
  }

  [Fact]
  public async Task Synchronize_IncompleteWithoutResumeFailsBeforeProviderCall()
  {
    var command = Command(new DateOnly(2012, 4, 1), new DateOnly(2012, 4, 2), 1);
    await CleanRangeAsync(command);
    await SeedStateAsync(command, CatalogSyncStatus.Paused, command.From);
    var provider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));

    await Assert.ThrowsAsync<CatalogUsageException>(() =>
      CreateService(provider).SynchronizeAsync(command, CancellationToken.None));

    Assert.Empty(provider.Requests);
  }

  [Fact]
  public async Task Synchronize_CompletedRangeIsIdempotentWithoutProviderCall()
  {
    var command = Command(new DateOnly(2012, 5, 1), new DateOnly(2012, 5, 2), 2);
    await CleanRangeAsync(command);
    var firstProvider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));
    await CreateService(firstProvider).SynchronizeAsync(command, CancellationToken.None);
    var secondProvider = new FakeCatalogClient((_, _, _) =>
      throw new InvalidOperationException("Completed sync must not call NASA."));

    var result = await CreateService(secondProvider).SynchronizeAsync(
      command,
      CancellationToken.None);

    Assert.True(result.WasAlreadyCompleted);
    Assert.Empty(secondProvider.Requests);
  }

  [Fact]
  public async Task Synchronize_CompletedDriftRequiresResumeAndReplaysFullRange()
  {
    var command = Command(new DateOnly(2012, 5, 10), new DateOnly(2012, 5, 12), 2);
    await CleanRangeAsync(command);
    var initialProvider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));
    await CreateService(initialProvider).SynchronizeAsync(command, CancellationToken.None);
    await using (var context = database.CreateDbContext())
    {
      await context.ApodEntries
        .Where(entry => entry.Date == new DateOnly(2012, 5, 11))
        .ExecuteDeleteAsync();
    }

    var repairProvider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));
    await Assert.ThrowsAsync<CatalogUsageException>(() =>
      CreateService(repairProvider).SynchronizeAsync(command, CancellationToken.None));
    Assert.Empty(repairProvider.Requests);

    await CreateService(repairProvider).SynchronizeAsync(
      command with { Resume = true },
      CancellationToken.None);

    Assert.Equal(command.From, repairProvider.Requests[0].From);
    await using var repairedContext = database.CreateDbContext();
    var repairedState = await FindStateAsync(repairedContext, command);
    Assert.Equal(3, repairedState.SyncedEntryCount);
    Assert.Equal(3, await repairedContext.ApodEntries.CountAsync(
      entry => entry.Date >= command.From && entry.Date <= command.To));
  }

  [Fact]
  public async Task Synchronize_RateLimitPausesWithoutAdvancingCheckpoint()
  {
    var command = Command(new DateOnly(2012, 6, 1), new DateOnly(2012, 6, 2), 2);
    await CleanRangeAsync(command);
    var provider = new FakeCatalogClient((_, _, _) =>
      Task.FromException<IReadOnlyList<ApodEntryDto>>(
        new CatalogNasaException(
          CatalogNasaFailure.RateLimited,
          Clock.GetUtcNow().AddMinutes(5))));

    await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateService(provider).SynchronizeAsync(command, CancellationToken.None));

    await using var context = database.CreateDbContext();
    var state = await FindStateAsync(context, command);
    Assert.Equal(CatalogSyncStatus.Paused, state.Status);
    Assert.Null(state.LastCompletedDate);
    Assert.Equal(Clock.GetUtcNow().AddMinutes(5), state.RetryNotBefore);
    Assert.Contains("resume no earlier", state.LastError, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Synchronize_ResumeBeforeRetryWindowIsRejectedWithoutProviderCall()
  {
    var command = Command(new DateOnly(2012, 6, 10), new DateOnly(2012, 6, 11), 2);
    await CleanRangeAsync(command);
    await SeedStateAsync(
      command,
      CatalogSyncStatus.Paused,
      checkpoint: null,
      retryNotBefore: Clock.GetUtcNow().AddMinutes(10));
    var provider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));

    await Assert.ThrowsAsync<CatalogSafetyException>(() =>
      CreateService(provider).SynchronizeAsync(
        command with { Resume = true },
        CancellationToken.None));

    Assert.Empty(provider.Requests);
  }

  [Theory]
  [InlineData(CatalogNasaFailure.Transient, CatalogSyncStatus.Paused, 20)]
  [InlineData(CatalogNasaFailure.Timeout, CatalogSyncStatus.Paused, 22)]
  [InlineData(CatalogNasaFailure.Permanent, CatalogSyncStatus.Failed, 24)]
  [InlineData(CatalogNasaFailure.InvalidPayload, CatalogSyncStatus.Failed, 26)]
  public async Task Synchronize_ProviderFailureClassificationIsPersisted(
    CatalogNasaFailure failure,
    CatalogSyncStatus expectedStatus,
    int day)
  {
    var command = Command(new DateOnly(2012, 6, day), new DateOnly(2012, 6, day), 1);
    await CleanRangeAsync(command);
    var provider = new FakeCatalogClient((_, _, _) =>
      Task.FromException<IReadOnlyList<ApodEntryDto>>(new CatalogNasaException(failure)));

    await Assert.ThrowsAsync<CatalogNasaException>(() =>
      CreateService(provider).SynchronizeAsync(command, CancellationToken.None));

    await using var context = database.CreateDbContext();
    var state = await FindStateAsync(context, command);
    Assert.Equal(expectedStatus, state.Status);
    Assert.Null(state.LastCompletedDate);
  }

  [Fact]
  public async Task Synchronize_GlobalLockRejectsOverlappingRange()
  {
    var heldRange = Command(new DateOnly(2012, 7, 1), new DateOnly(2012, 7, 2), 2);
    var overlapping = Command(new DateOnly(2012, 7, 2), new DateOnly(2012, 7, 3), 2);
    await CleanRangeAsync(heldRange);
    await CleanRangeAsync(overlapping);
    await using var heldLock = await CatalogGlobalLock.AcquireAsync(
      database.ConnectionString,
      TimeSpan.FromSeconds(1),
      CancellationToken.None);
    var provider = new FakeCatalogClient((from, to, _) =>
      Task.FromResult<IReadOnlyList<ApodEntryDto>>(Entries(from, to)));

    await Assert.ThrowsAsync<CatalogConcurrencyException>(() =>
      CreateService(provider).SynchronizeAsync(overlapping, CancellationToken.None));

    Assert.Empty(provider.Requests);
  }

  [Fact]
  public async Task Synchronize_LostLockHeartbeatPausesWithoutCheckpoint()
  {
    var command = Command(new DateOnly(2012, 8, 1), new DateOnly(2012, 8, 2), 2);
    await CleanRangeAsync(command);
    var providerStarted = new TaskCompletionSource(
      TaskCreationOptions.RunContinuationsAsynchronously);
    var provider = new FakeCatalogClient(async (_, _, cancellationToken) =>
    {
      providerStarted.TrySetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      return [];
    });
    var synchronization = CreateService(provider, TimeSpan.FromMilliseconds(50))
      .SynchronizeAsync(command, CancellationToken.None);
    await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    await using (var connection = new Npgsql.NpgsqlConnection(database.ConnectionString))
    {
      await connection.OpenAsync();
      await using var terminate = new Npgsql.NpgsqlCommand(
        """
        SELECT bool_or(pg_terminate_backend(pid))
        FROM pg_locks
        WHERE locktype = 'advisory'
          AND granted
          AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
          AND pid <> pg_backend_pid();
        """,
        connection);
      Assert.True((bool)(await terminate.ExecuteScalarAsync() ?? false));
    }

    await Assert.ThrowsAsync<CatalogConcurrencyException>(async () =>
      await synchronization.WaitAsync(TimeSpan.FromSeconds(10)));

    await using var context = database.CreateDbContext();
    var state = await FindStateAsync(context, command);
    Assert.Equal(CatalogSyncStatus.Paused, state.Status);
    Assert.Null(state.LastCompletedDate);
    Assert.Equal(0, state.SyncedEntryCount);
  }

  private CatalogSyncService CreateService(
    INasaCatalogClient provider,
    TimeSpan? heartbeatInterval = null) => new(
    database.ConnectionString,
    database.CreateDbContext,
    provider,
    Clock,
    heartbeatInterval);

  private static CatalogSyncCommand Command(DateOnly from, DateOnly to, int batchSize) => new(
    from,
    to,
    batchSize,
    Resume: false,
    DryRun: false,
    AllowLocalProduction: false);

  private async Task CleanRangeAsync(CatalogSyncCommand command)
  {
    await using var context = database.CreateDbContext();
    await context.CatalogSyncStates.Where(
      state => state.TargetFrom == command.From && state.TargetTo == command.To)
      .ExecuteDeleteAsync();
    await context.ApodEntries.Where(
      entry => entry.Date >= command.From && entry.Date <= command.To)
      .ExecuteDeleteAsync();
  }

  private async Task SeedStateAsync(
    CatalogSyncCommand command,
    CatalogSyncStatus status,
    DateOnly? checkpoint,
    DateTimeOffset? retryNotBefore = null)
  {
    await using var context = database.CreateDbContext();
    context.CatalogSyncStates.Add(new CatalogSyncState
    {
      Id = Guid.NewGuid(),
      TargetFrom = command.From,
      TargetTo = command.To,
      LastCompletedDate = checkpoint,
      RetryNotBefore = retryNotBefore,
      Status = status,
      CreatedAt = Clock.GetUtcNow(),
      UpdatedAt = Clock.GetUtcNow()
    });
    await context.SaveChangesAsync();
  }

  private static Task<CatalogSyncState> FindStateAsync(
    AstronomyExplorer.Api.Data.AppDbContext context,
    CatalogSyncCommand command) => context.CatalogSyncStates.AsNoTracking().SingleAsync(
      state => state.TargetFrom == command.From && state.TargetTo == command.To);

  private static IReadOnlyList<ApodEntryDto> Entries(DateOnly from, DateOnly to) =>
    Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
      .Select(offset => Entry(from.AddDays(offset)))
      .ToArray();

  private static ApodEntryDto Entry(DateOnly date) => new(
    date,
    $"APOD {date:yyyy-MM-dd}",
    "Catalog entry.",
    "image",
    $"https://images.example/{date:yyyy-MM-dd}.jpg",
    null,
    null,
    null);

  private sealed class FakeCatalogClient(
    Func<DateOnly, DateOnly, CancellationToken, Task<IReadOnlyList<ApodEntryDto>>> response)
    : INasaCatalogClient
  {
    public List<(DateOnly From, DateOnly To)> Requests { get; } = [];

    public Task<IReadOnlyList<ApodEntryDto>> FetchRangeAsync(
      DateOnly from,
      DateOnly to,
      CancellationToken cancellationToken)
    {
      Requests.Add((from, to));
      return response(from, to, cancellationToken);
    }
  }

  private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => utcNow;
  }
}
