using System.Collections.Concurrent;
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

  public AccountApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    TimeSpan? confirmationTokenLifespan = null)
  {
    _connectionString = connectionString;
    _settings = settings ?? new Dictionary<string, string?>();
    _confirmationTokenLifespan = confirmationTokenLifespan;
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

      if (_confirmationTokenLifespan is { } tokenLifespan)
      {
        services.Configure<DataProtectionTokenProviderOptions>(options =>
          options.TokenLifespan = tokenLifespan);
      }
    });
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
