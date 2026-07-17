namespace AstronomyExplorer.Api.Email;

public interface IEmailSender
{
  Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
