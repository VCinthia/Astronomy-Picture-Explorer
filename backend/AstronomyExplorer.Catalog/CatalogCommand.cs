using System.Globalization;

namespace AstronomyExplorer.Catalog;

public sealed record CatalogSyncCommand(
  DateOnly From,
  DateOnly To,
  int BatchSize,
  bool Resume,
  bool DryRun,
  bool AllowLocalProduction)
{
  public const int DefaultBatchSize = 30;
  public const int MaximumBatchSize = 30;
  public static readonly DateOnly FirstApodDate = new(1995, 6, 16);

  public int DateCount => To.DayNumber - From.DayNumber + 1;

  public int EstimatedRequestCount =>
    (int)Math.Ceiling(DateCount / (double)BatchSize);
}

public static class CatalogCommandParser
{
  public static CatalogSyncCommand Parse(string[] args, DateOnly latestSupportedDate)
  {
    if (args.Length < 2 || args[0] != "catalog" || args[1] != "sync")
    {
      throw new CatalogUsageException(
        "Expected: catalog sync --from YYYY-MM-DD --to YYYY-MM-DD [options].");
    }

    string? fromValue = null;
    string? toValue = null;
    var batchSize = CatalogSyncCommand.DefaultBatchSize;
    var resume = false;
    var dryRun = false;
    var allowLocalProduction = false;
    var seenOptions = new HashSet<string>(StringComparer.Ordinal);

    for (var index = 2; index < args.Length; index++)
    {
      switch (args[index])
      {
        case "--from":
          EnsureNotDuplicate(seenOptions, args[index]);
          fromValue = ReadValue(args, ref index, "--from");
          break;
        case "--to":
          EnsureNotDuplicate(seenOptions, args[index]);
          toValue = ReadValue(args, ref index, "--to");
          break;
        case "--batch-size":
          EnsureNotDuplicate(seenOptions, args[index]);
          var batchValue = ReadValue(args, ref index, "--batch-size");
          if (!int.TryParse(batchValue, CultureInfo.InvariantCulture, out batchSize))
          {
            throw new CatalogUsageException("--batch-size must be an integer from 1 to 30.");
          }

          break;
        case "--resume":
          EnsureNotDuplicate(seenOptions, args[index]);
          resume = true;
          break;
        case "--dry-run":
          EnsureNotDuplicate(seenOptions, args[index]);
          dryRun = true;
          break;
        case "--allow-local-production":
          EnsureNotDuplicate(seenOptions, args[index]);
          allowLocalProduction = true;
          break;
        default:
          throw new CatalogUsageException($"Unknown option: {args[index]}.");
      }
    }

    var from = ParseDate(fromValue, "--from");
    var to = ParseDate(toValue, "--to");
    if (from < CatalogSyncCommand.FirstApodDate || from > to || to > latestSupportedDate)
    {
      throw new CatalogUsageException(
        $"Range must be within {CatalogSyncCommand.FirstApodDate:yyyy-MM-dd} and the " +
        $"latest supported APOD date ({latestSupportedDate:yyyy-MM-dd}).");
    }

    if (batchSize is < 1 or > CatalogSyncCommand.MaximumBatchSize)
    {
      throw new CatalogUsageException("--batch-size must be from 1 to 30.");
    }

    return new CatalogSyncCommand(
      from,
      to,
      batchSize,
      resume,
      dryRun,
      allowLocalProduction);
  }

  private static string ReadValue(string[] args, ref int index, string option)
  {
    if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
    {
      throw new CatalogUsageException($"{option} requires a value.");
    }

    return args[index];
  }

  private static DateOnly ParseDate(string? value, string option)
  {
    if (!DateOnly.TryParseExact(
          value,
          "yyyy-MM-dd",
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out var date))
    {
      throw new CatalogUsageException($"{option} must use YYYY-MM-DD.");
    }

    return date;
  }

  private static void EnsureNotDuplicate(HashSet<string> seenOptions, string option)
  {
    if (!seenOptions.Add(option))
    {
      throw new CatalogUsageException($"Duplicate option: {option}.");
    }
  }
}

public sealed class CatalogUsageException(string message) : Exception(message);
