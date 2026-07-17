using System.Text;
using System.Text.Encodings.Web;
using AstronomyExplorer.Api.Auth.Dtos;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Email;
using AstronomyExplorer.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AstronomyExplorer.Api.Auth;

public static class AccountEndpoints
{
  private const string AcceptedMessage =
    "If the address can receive a confirmation email, a message will be sent.";
  private const string ConfirmationEmailSubject = "Confirm your Astronomy Explorer account";
  private const int MaxIdentityEmailLength = 256;

  public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup("/auth")
      .WithTags("Account");

    group.MapPost("/register", RegisterAsync)
      .WithName("RegisterAccount")
      .RequireRateLimiting(AccountRateLimitPolicies.RegisterByIp);

    group.MapPost("/resend-confirmation", ResendConfirmationAsync)
      .WithName("ResendAccountConfirmation")
      .RequireRateLimiting(AccountRateLimitPolicies.ResendConfirmationByIp);

    group.MapPost("/confirm-email", ConfirmEmailAsync)
      .WithName("ConfirmAccountEmail");

    return endpoints;
  }

  private static async Task<IResult> RegisterAsync(
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    IAccountEmailRateLimiter emailRateLimiter,
    EmailConfirmationLinkFactory linkFactory,
    IEmailSender emailSender,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
  {
    var email = request.Email?.Trim() ?? string.Empty;
    var normalizedEmail = NormalizeEmailForRateLimit(userManager, email);
    if (!emailRateLimiter.TryAcquireRegistration(normalizedEmail))
    {
      return Results.Problem(AccountRateLimitProblemDetails.Create());
    }

    var validationErrors = ValidateRegistration(email, request.Password);
    if (validationErrors.Count > 0)
    {
      return Results.ValidationProblem(validationErrors);
    }

    var user = new ApplicationUser
    {
      Email = email,
      UserName = email
    };

    IdentityResult creationResult;
    try
    {
      creationResult = await userManager.CreateAsync(user, request.Password!);
    }
    catch (DbUpdateException exception) when (
      exception.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation
      })
    {
      return Accepted();
    }

    if (!creationResult.Succeeded)
    {
      if (creationResult.Errors.Any(IsDuplicateIdentityError))
      {
        return Accepted();
      }

      return Results.ValidationProblem(
        creationResult.Errors
          .GroupBy(error => error.Code)
          .ToDictionary(
            group => group.Key,
            group => group.Select(error => error.Description).ToArray()));
    }

    await TrySendConfirmationEmailAsync(
      user,
      userManager,
      linkFactory,
      emailSender,
      loggerFactory,
      cancellationToken);

    return Accepted();
  }

  private static async Task<IResult> ResendConfirmationAsync(
    ResendConfirmationRequest request,
    UserManager<ApplicationUser> userManager,
    IAccountEmailRateLimiter emailRateLimiter,
    EmailConfirmationLinkFactory linkFactory,
    IEmailSender emailSender,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
  {
    var email = request.Email?.Trim() ?? string.Empty;
    var normalizedEmail = NormalizeEmailForRateLimit(userManager, email);
    if (!emailRateLimiter.TryAcquireConfirmationResend(normalizedEmail))
    {
      return Results.Problem(AccountRateLimitProblemDetails.Create());
    }

    if (email.Length is 0 or > MaxIdentityEmailLength)
    {
      return Accepted();
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
    {
      await TrySendConfirmationEmailAsync(
        user,
        userManager,
        linkFactory,
        emailSender,
        loggerFactory,
        cancellationToken);
    }

    return Accepted();
  }

  private static async Task<IResult> ConfirmEmailAsync(
    ConfirmEmailRequest request,
    UserManager<ApplicationUser> userManager)
  {
    if (!IsBase64Url(request.Code))
    {
      return InvalidConfirmation();
    }

    string token;
    try
    {
      token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code!));
    }
    catch (FormatException)
    {
      return InvalidConfirmation();
    }

    var user = await userManager.FindByIdAsync(request.UserId.ToString());
    if (user is null)
    {
      return InvalidConfirmation();
    }

    if (await userManager.IsEmailConfirmedAsync(user))
    {
      return InvalidConfirmation();
    }

    var confirmationResult = await userManager.ConfirmEmailAsync(user, token);
    return confirmationResult.Succeeded
      ? Results.NoContent()
      : InvalidConfirmation();
  }

  private static async Task TrySendConfirmationEmailAsync(
    ApplicationUser user,
    UserManager<ApplicationUser> userManager,
    EmailConfirmationLinkFactory linkFactory,
    IEmailSender emailSender,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
  {
    var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
    var base64UrlCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    var confirmationLink = linkFactory.Create(user.Id, base64UrlCode);
    var encodedLink = HtmlEncoder.Default.Encode(confirmationLink);
    var email = new EmailMessage(
      user.Email!,
      ConfirmationEmailSubject,
      $"<p>Confirm your email to finish creating your Astronomy Explorer account.</p>" +
      $"<p><a href=\"{encodedLink}\">Confirm email</a></p>");

    try
    {
      await emailSender.SendAsync(email, cancellationToken);
    }
    catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
    {
      loggerFactory
        .CreateLogger("AstronomyExplorer.Api.EmailConfirmation")
        .LogWarning(
          "Confirmation email delivery failed with {ExceptionType}.",
          exception.GetType().Name);
    }
  }

  private static Dictionary<string, string[]> ValidateRegistration(
    string email,
    string? password)
  {
    var errors = new Dictionary<string, string[]>();
    if (email.Length is 0 or > MaxIdentityEmailLength)
    {
      errors["email"] = ["A valid email address is required."];
    }

    if (string.IsNullOrEmpty(password))
    {
      errors["password"] = ["A password is required."];
    }

    return errors;
  }

  private static bool IsDuplicateIdentityError(IdentityError error) =>
    error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or
      nameof(IdentityErrorDescriber.DuplicateUserName);

  private static string NormalizeEmailForRateLimit(
    UserManager<ApplicationUser> userManager,
    string email) => email.Length <= MaxIdentityEmailLength
      ? userManager.NormalizeEmail(email) ?? string.Empty
      : "invalid-email";

  private static bool IsBase64Url(string? code) =>
    !string.IsNullOrWhiteSpace(code) &&
    code.Length <= 4096 &&
    code.All(character =>
      char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

  private static IResult Accepted() => Results.Accepted(
    value: new AccountRequestAcceptedResponse(AcceptedMessage));

  private static IResult InvalidConfirmation() => Results.Problem(
    statusCode: StatusCodes.Status400BadRequest,
    title: "Unable to confirm email.",
    detail: "The confirmation request is invalid or has expired.",
    type: "https://httpstatuses.com/400");
}
