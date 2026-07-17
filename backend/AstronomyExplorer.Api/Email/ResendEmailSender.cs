using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Email;

public sealed class ResendEmailSender(
  HttpClient httpClient,
  IOptions<ResendEmailOptions> options) : IEmailSender
{
  private readonly ResendEmailOptions _options = options.Value;

  public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
        string.IsNullOrWhiteSpace(_options.FromAddress))
    {
      throw new InvalidOperationException(
        "Resend:ApiKey and Resend:FromAddress must be configured before sending email.");
    }

    using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
    {
      Content = JsonContent.Create(new ResendEmailRequest(
        _options.FromAddress,
        [message.Recipient],
        message.Subject,
        message.HtmlBody))
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

    using var response = await httpClient.SendAsync(request, cancellationToken);
    response.EnsureSuccessStatusCode();
  }

  private sealed record ResendEmailRequest(
    string From,
    string[] To,
    string Subject,
    string Html);
}
