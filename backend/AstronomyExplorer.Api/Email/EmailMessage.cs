namespace AstronomyExplorer.Api.Email;

public sealed record EmailMessage(
  string Recipient,
  string Subject,
  string HtmlBody);
