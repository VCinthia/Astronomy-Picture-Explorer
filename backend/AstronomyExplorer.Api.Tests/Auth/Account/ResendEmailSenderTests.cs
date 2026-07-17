using System.Net;
using System.Text.Json;
using AstronomyExplorer.Api.Email;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Tests.Auth.Account;

public sealed class ResendEmailSenderTests
{
  [Fact]
  public async Task SendAsync_ConfiguredMessage_UsesOfficialResendHttpContract()
  {
    var handler = new RecordingHttpMessageHandler();
    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.resend.com/")
    };
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AstronomyExplorer/1.0");
    var options = Options.Create(new ResendEmailOptions
    {
      ApiKey = "test-api-key",
      FromAddress = "Astronomy Explorer <noreply@example.test>"
    });
    var sender = new ResendEmailSender(httpClient, options);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await sender.SendAsync(
      new EmailMessage(
        "recipient@example.test",
        "Confirm account",
        "<p>Confirmation</p>"),
      cancellation.Token);

    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("https://api.resend.com/emails", handler.RequestUri?.AbsoluteUri);
    Assert.Equal("Bearer", handler.AuthorizationScheme);
    Assert.Equal("test-api-key", handler.AuthorizationParameter);
    Assert.False(string.IsNullOrWhiteSpace(handler.UserAgent));
    using var payload = JsonDocument.Parse(Assert.IsType<string>(handler.Content));
    Assert.Equal(
      "Astronomy Explorer <noreply@example.test>",
      payload.RootElement.GetProperty("from").GetString());
    Assert.Equal(
      "recipient@example.test",
      payload.RootElement.GetProperty("to")[0].GetString());
    Assert.Equal("Confirm account", payload.RootElement.GetProperty("subject").GetString());
    Assert.Equal("<p>Confirmation</p>", payload.RootElement.GetProperty("html").GetString());
  }

  private sealed class RecordingHttpMessageHandler : HttpMessageHandler
  {
    public HttpMethod? Method { get; private set; }

    public Uri? RequestUri { get; private set; }

    public string? AuthorizationScheme { get; private set; }

    public string? AuthorizationParameter { get; private set; }

    public string? UserAgent { get; private set; }

    public string? Content { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      Method = request.Method;
      RequestUri = request.RequestUri;
      AuthorizationScheme = request.Headers.Authorization?.Scheme;
      AuthorizationParameter = request.Headers.Authorization?.Parameter;
      UserAgent = request.Headers.UserAgent.ToString();
      Content = request.Content is null
        ? null
        : await request.Content.ReadAsStringAsync(cancellationToken);

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent("{\"id\":\"email-id\"}")
      };
    }
  }
}
