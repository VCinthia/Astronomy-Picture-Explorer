using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Catalog;

public static class CatalogProgram
{
  public static async Task<int> Main(string[] args)
  {
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
      eventArgs.Cancel = true;
      cancellation.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;
    try
    {
      return await RunAsync(
        args,
        Environment.GetEnvironmentVariable,
        Console.Out,
        Console.Error,
        DateOnly.FromDateTime(DateTime.UtcNow),
        cancellation.Token);
    }
    finally
    {
      Console.CancelKeyPress -= cancelHandler;
    }
  }

  public static async Task<int> RunAsync(
    string[] args,
    Func<string, string?> readEnvironment,
    TextWriter output,
    TextWriter error,
    DateOnly todayUtc,
    CancellationToken cancellationToken)
  {
    try
    {
      var command = CatalogCommandParser.Parse(args, todayUtc);
      await WritePreflightAsync(output, command);
      if (command.DryRun)
      {
        await output.WriteLineAsync("Dry run complete. No database or NASA request was opened.");
        return 0;
      }

      var settings = CatalogPreflight.ValidateLive(command, readEnvironment);
      using var httpClient = new HttpClient(new HttpClientHandler
      {
        AllowAutoRedirect = false
      })
      {
        BaseAddress = new Uri("https://api.nasa.gov/"),
        Timeout = TimeSpan.FromSeconds(8)
      };
      httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AstronomyExplorer.Catalog/1.0");

      var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(settings.ConnectionString)
        .Options;
      var nasaClient = new NasaCatalogClient(
        httpClient,
        settings.NasaApiKey,
        new CatalogRetryDelay(),
        TimeProvider.System);
      var service = new CatalogSyncService(
        settings.ConnectionString,
        () => new AppDbContext(dbOptions),
        nasaClient,
        TimeProvider.System);
      var result = await service.SynchronizeAsync(command, cancellationToken);

      await output.WriteLineAsync(result.WasAlreadyCompleted
        ? "Catalog range was already complete; no provider request was made."
        : $"Catalog synchronization completed through {result.LastCompletedDate:yyyy-MM-dd} " +
          $"in {result.CompletedBatches} batch(es).");
      return 0;
    }
    catch (CatalogUsageException exception)
    {
      await error.WriteLineAsync(exception.Message);
      return 2;
    }
    catch (CatalogSafetyException exception)
    {
      await error.WriteLineAsync(exception.Message);
      return 3;
    }
    catch (CatalogConcurrencyException exception)
    {
      await error.WriteLineAsync(exception.Message);
      return 4;
    }
    catch (CatalogNasaException exception)
    {
      await error.WriteLineAsync(exception.Failure == CatalogNasaFailure.RateLimited
        ? FormatRateLimit(exception.RetryNotBefore)
        : "Catalog provider request failed. Resume is safe.");
      return 5;
    }
    catch (OperationCanceledException)
    {
      await error.WriteLineAsync("Catalog synchronization paused. Resume is safe.");
      return 130;
    }
    catch
    {
      await error.WriteLineAsync(
        "Catalog synchronization failed without advancing the current batch checkpoint.");
      return 1;
    }
  }

  private static async Task WritePreflightAsync(
    TextWriter output,
    CatalogSyncCommand command)
  {
    await output.WriteLineAsync(
      $"Range: {command.From:yyyy-MM-dd}..{command.To:yyyy-MM-dd}");
    await output.WriteLineAsync(
      $"Dates: {command.DateCount}; batch size: {command.BatchSize}; " +
      $"estimated NASA requests: {command.EstimatedRequestCount}.");
    await output.WriteLineAsync($"Resume: {command.Resume}; dry run: {command.DryRun}.");
  }

  private static string FormatRateLimit(DateTimeOffset? retryNotBefore) =>
    retryNotBefore is { } instant
    ? $"NASA rate limit reached. Resume no earlier than {instant:O}."
    : "NASA rate limit reached. Resume after the provider window resets.";
}
