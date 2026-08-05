using System.Collections.Concurrent;
using AstronomyExplorer.Api.Auth;
using AstronomyExplorer.Api.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AstronomyExplorer.Api.Tests.Auth.Account;

public sealed class AccountApiFactory : WebApplicationFactory<Program>
{
  private readonly string _connectionString;
  private readonly IReadOnlyDictionary<string, string?> _settings;
  private readonly TimeSpan? _confirmationTokenLifespan;
  private readonly IUserSessionLock? _userSessionLock;

  public AccountApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    TimeSpan? confirmationTokenLifespan = null,
    IUserSessionLock? userSessionLock = null)
  {
    _connectionString = connectionString;
    _settings = settings ?? new Dictionary<string, string?>();
    _confirmationTokenLifespan = confirmationTokenLifespan;
    _userSessionLock = userSessionLock;
  }

  public FakeEmailSender EmailSender { get; } = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Testing");
    builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
    builder.UseSetting("Frontend:PublicBaseUrl", "https://portfolio.example");
    builder.UseSetting("Session:Issuer", "https://api.example.test");
    builder.UseSetting("Session:Audience", "astronomy-explorer-tests");
    builder.UseSetting("Session:SigningKey", "test-signing-key-at-least-32-bytes-long");
    builder.UseSetting("NasaApod:ApiKey", "test-nasa-api-key");

    foreach (var setting in _settings)
    {
      builder.UseSetting(setting.Key, setting.Value);
    }

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<IEmailSender>();
      services.AddSingleton<IEmailSender>(EmailSender);

      if (_userSessionLock is not null)
      {
        services.RemoveAll<IUserSessionLock>();
        services.AddSingleton(_userSessionLock);
      }

      if (_confirmationTokenLifespan is { } tokenLifespan)
      {
        services.Configure<DataProtectionTokenProviderOptions>(options =>
          options.TokenLifespan = tokenLifespan);
      }
    });
  }
}

public sealed class GatedUserSessionLock : IUserSessionLock
{
  private readonly TaskCompletionSource _firstAcquisition = new(
    TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly TaskCompletionSource _releaseFirstAcquisition = new(
    TaskCreationOptions.RunContinuationsAsynchronously);
  private int _armed;
  private int _acquisitionCount;

  public void Arm() => Volatile.Write(ref _armed, 1);

  public Task WaitForFirstAcquisitionAsync() => _firstAcquisition.Task;

  public void ReleaseFirstAcquisition() => _releaseFirstAcquisition.TrySetResult();

  public Task AcquireAsync(Guid userId, CancellationToken cancellationToken)
  {
    if (Volatile.Read(ref _armed) == 0 || Interlocked.Increment(ref _acquisitionCount) != 1)
    {
      return Task.CompletedTask;
    }

    _firstAcquisition.TrySetResult();
    return _releaseFirstAcquisition.Task.WaitAsync(cancellationToken);
  }
}

public sealed class FakeEmailSender : IEmailSender
{
  private readonly ConcurrentQueue<EmailMessage> _messages = new();

  public IReadOnlyList<EmailMessage> Messages => _messages.ToArray();

  public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    _messages.Enqueue(message);
    return Task.CompletedTask;
  }

  public void Clear()
  {
    while (_messages.TryDequeue(out _))
    {
    }
  }
}
