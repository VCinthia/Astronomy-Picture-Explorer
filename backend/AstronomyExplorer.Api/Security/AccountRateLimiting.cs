using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace AstronomyExplorer.Api.Security;

public sealed class AccountRateLimitOptions
{
  public const string SectionName = "AccountRateLimits";

  public int RegisterIpPermitLimit { get; init; } = 5;

  public int RegisterEmailPermitLimit { get; init; } = 3;

  public int ResendConfirmationIpPermitLimit { get; init; } = 5;

  public int ResendConfirmationEmailPermitLimit { get; init; } = 3;

  public int LoginIpPermitLimit { get; init; } = 10;

  public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(15);

  public int MaxTrackedEmailPartitions { get; init; } = 10_000;
}

public static class AccountRateLimitPolicies
{
  public const string RegisterByIp = "register-by-ip";
  public const string ResendConfirmationByIp = "resend-confirmation-by-ip";
  public const string LoginByIp = "login-by-ip";

  public static FixedWindowRateLimiterOptions CreateFixedWindowOptions(
    int permitLimit,
    TimeSpan window)
  {
    return new FixedWindowRateLimiterOptions
    {
      PermitLimit = Math.Max(1, permitLimit),
      Window = window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(15),
      QueueLimit = 0,
      AutoReplenishment = true
    };
  }
}

public static class AccountRateLimitPartitionKeys
{
  public static string FromRemoteIp(HttpContext httpContext) =>
    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public static class AccountRateLimitProblemDetails
{
  public static async ValueTask WriteAsync(
    OnRejectedContext context,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    context.HttpContext.Response.Headers.CacheControl = "no-store";
    context.HttpContext.Response.Headers.Pragma = "no-cache";
    await Results.Problem(Create()).ExecuteAsync(context.HttpContext);
  }

  public static ProblemDetails Create() => new()
  {
    Status = StatusCodes.Status429TooManyRequests,
    Title = "Too many account requests.",
    Detail = "Please wait before trying again.",
    Type = "https://httpstatuses.com/429"
  };
}

public interface IAccountEmailRateLimiter
{
  bool TryAcquireRegistration(string normalizedEmail);

  bool TryAcquireConfirmationResend(string normalizedEmail);
}

public sealed class AccountEmailRateLimiter : IAccountEmailRateLimiter, IDisposable
{
  private readonly object _gate = new();
  private readonly MemoryCache _partitions;
  private readonly AccountRateLimitOptions _options;
  private readonly TimeProvider _timeProvider;

  public AccountEmailRateLimiter(
    IOptions<AccountRateLimitOptions> options,
    TimeProvider timeProvider)
  {
    _options = options.Value;
    _timeProvider = timeProvider;
    _partitions = new MemoryCache(new MemoryCacheOptions
    {
      SizeLimit = Math.Max(1, _options.MaxTrackedEmailPartitions)
    });
  }

  public bool TryAcquireRegistration(string normalizedEmail) => TryAcquire(
    "register",
    normalizedEmail,
    _options.RegisterEmailPermitLimit);

  public bool TryAcquireConfirmationResend(string normalizedEmail) => TryAcquire(
    "resend",
    normalizedEmail,
    _options.ResendConfirmationEmailPermitLimit);

  public void Dispose() => _partitions.Dispose();

  private bool TryAcquire(string operation, string normalizedEmail, int permitLimit)
  {
    var now = _timeProvider.GetUtcNow();
    var window = _options.Window > TimeSpan.Zero
      ? _options.Window
      : TimeSpan.FromMinutes(15);
    var partitionKey = $"{operation}:{Hash(normalizedEmail)}";

    lock (_gate)
    {
      if (!_partitions.TryGetValue(partitionKey, out WindowCounter? counter) ||
          counter is null ||
          now >= counter.ExpiresAt)
      {
        counter = new WindowCounter(now.Add(window));
        _partitions.Set(
          partitionKey,
          counter,
          new MemoryCacheEntryOptions
          {
            AbsoluteExpiration = counter.ExpiresAt,
            Size = 1
          });
      }

      if (counter.Count >= Math.Max(1, permitLimit))
      {
        return false;
      }

      counter.Count++;
      return true;
    }
  }

  private static string Hash(string normalizedEmail)
  {
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
    return Convert.ToHexString(bytes);
  }

  private sealed class WindowCounter(DateTimeOffset expiresAt)
  {
    public int Count { get; set; }

    public DateTimeOffset ExpiresAt { get; } = expiresAt;
  }
}
