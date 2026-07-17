using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Email;

public sealed class EmailConfirmationLinkFactory(IOptions<FrontendOptions> options)
{
  private readonly Uri _frontendBaseUri = ValidateFrontendBaseUri(options.Value.PublicBaseUrl);

  public string Create(Guid userId, string base64UrlCode)
  {
    var relativeLink = QueryHelpers.AddQueryString(
      "/confirm-email",
      new Dictionary<string, string?>
      {
        ["userId"] = userId.ToString(),
        ["code"] = base64UrlCode
      });

    var link = new UriBuilder(_frontendBaseUri)
    {
      Path = "/confirm-email",
      Query = new Uri(relativeLink, UriKind.Relative).OriginalString.Split('?', 2)[1],
      Fragment = string.Empty
    };

    return link.Uri.AbsoluteUri;
  }

  private static Uri ValidateFrontendBaseUri(string value)
  {
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
        !string.IsNullOrEmpty(uri.UserInfo))
    {
      throw new InvalidOperationException(
        "Frontend:PublicBaseUrl must be an absolute HTTP or HTTPS URL without user information.");
    }

    return uri;
  }
}
