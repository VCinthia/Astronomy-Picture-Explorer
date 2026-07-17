using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Tests.Auth.Account;

[Collection(PostgreSqlCollection.Name)]
public sealed partial class AccountEndpointTests(PostgreSqlFixture database)
{
  private const string ValidPassword = "Valid1!Password";

  [Fact]
  public async Task RegisterThenConfirm_ValidCapturedLink_ConfirmsPersistedUser()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("confirm");

    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
    var sentEmail = Assert.Single(factory.EmailSender.Messages);
    var confirmationUri = ExtractConfirmationUri(sentEmail.HtmlBody);
    var query = QueryHelpers.ParseQuery(confirmationUri.Query);
    var userId = Assert.IsType<string>(query["userId"].ToString());
    var code = Assert.IsType<string>(query["code"].ToString());

    Assert.Equal("https", confirmationUri.Scheme);
    Assert.Equal("portfolio.example", confirmationUri.Host);
    Assert.Equal("/confirm-email", confirmationUri.AbsolutePath);
    Assert.Matches("^[A-Za-z0-9_-]+$", code);
    Assert.DoesNotContain('=', code);
    Assert.DoesNotContain('/', code);
    Assert.DoesNotContain('+', code);

    using var confirmationResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId, code },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.NoContent, confirmationResponse.StatusCode);
    await using var context = database.CreateDbContext();
    var user = await context.Users.SingleAsync(
      candidate => candidate.Email == email,
      cancellation.Token);
    Assert.True(user.EmailConfirmed);
  }

  [Fact]
  public async Task ConfirmEmail_NewApplicationInstance_UsesPersistedDataProtectionKeyRing()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("persisted-key-ring");
    Uri confirmationUri;

    await using (var registrationFactory = new AccountApiFactory(database.ConnectionString))
    {
      using var registrationClient = registrationFactory.CreateClient();
      using var registrationResponse = await RegisterAsync(
        registrationClient,
        email,
        ValidPassword,
        cancellation.Token);
      Assert.Equal(HttpStatusCode.Accepted, registrationResponse.StatusCode);
      confirmationUri = ExtractConfirmationUri(
        Assert.Single(registrationFactory.EmailSender.Messages).HtmlBody);
    }

    var query = QueryHelpers.ParseQuery(confirmationUri.Query);
    await using (var confirmationFactory = new AccountApiFactory(database.ConnectionString))
    {
      using var confirmationClient = confirmationFactory.CreateClient();
      using var confirmationResponse = await confirmationClient.PostAsJsonAsync(
        "/auth/confirm-email",
        new
        {
          userId = query["userId"].ToString(),
          code = query["code"].ToString()
        },
        cancellation.Token);
      Assert.Equal(HttpStatusCode.NoContent, confirmationResponse.StatusCode);
    }

    await using var context = database.CreateDbContext();
    var user = await context.Users.SingleAsync(
      candidate => candidate.Email == email,
      cancellation.Token);
    Assert.True(user.EmailConfirmed);
    Assert.True(await context.DataProtectionKeys.AnyAsync(cancellation.Token));
  }

  [Fact]
  public async Task Register_DuplicateNormalizedEmail_ReturnsSameGenericResponseWithoutExtraEmail()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("duplicate");

    using var firstResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    var firstBody = await firstResponse.Content.ReadAsStringAsync(cancellation.Token);
    using var duplicateResponse = await RegisterAsync(
      client,
      email.ToUpperInvariant(),
      ValidPassword,
      cancellation.Token);
    var duplicateBody = await duplicateResponse.Content.ReadAsStringAsync(cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, duplicateResponse.StatusCode);
    Assert.Equal(firstBody, duplicateBody);
    Assert.Single(factory.EmailSender.Messages);
  }

  [Fact]
  public async Task AccountEmail_ExceedsIdentityColumnLength_IsHandledWithoutPersistenceOrEmail()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var oversizedEmail = $"{new string('a', 244)}@example.test";
    Assert.Equal(257, oversizedEmail.Length);

    using var registerResponse = await RegisterAsync(
      client,
      oversizedEmail,
      ValidPassword,
      cancellation.Token);
    using var resendResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email = oversizedEmail },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode);
    Assert.Equal("application/problem+json", registerResponse.Content.Headers.ContentType?.MediaType);
    Assert.Equal(HttpStatusCode.Accepted, resendResponse.StatusCode);
    Assert.Empty(factory.EmailSender.Messages);
    await using var context = database.CreateDbContext();
    Assert.False(await context.Users.AnyAsync(
      user => user.Email == oversizedEmail,
      cancellation.Token));
  }

  [Fact]
  public async Task ResendConfirmation_UnconfirmedUser_SendsNewConfirmationEmail()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("resend");
    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    registerResponse.EnsureSuccessStatusCode();
    factory.EmailSender.Clear();

    using var resendResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, resendResponse.StatusCode);
    Assert.Single(factory.EmailSender.Messages);
  }

  [Fact]
  public async Task ResendConfirmation_NonexistentOrConfirmedUser_ReturnsSameGenericResponseWithoutEmail()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("confirmed-resend");
    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    registerResponse.EnsureSuccessStatusCode();
    var link = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var query = QueryHelpers.ParseQuery(link.Query);
    using var confirmationResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId = query["userId"].ToString(), code = query["code"].ToString() },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, confirmationResponse.StatusCode);
    factory.EmailSender.Clear();

    using var confirmedResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email },
      cancellation.Token);
    var confirmedBody = await confirmedResponse.Content.ReadAsStringAsync(cancellation.Token);
    using var nonexistentResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email = UniqueEmail("missing") },
      cancellation.Token);
    var nonexistentBody = await nonexistentResponse.Content.ReadAsStringAsync(cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, confirmedResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, nonexistentResponse.StatusCode);
    Assert.Equal(confirmedBody, nonexistentBody);
    Assert.Empty(factory.EmailSender.Messages);
  }

  [Fact]
  public async Task ConfirmEmail_InvalidReusedOrNonexistentRequest_ReturnsSameControlledProblem()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("reuse");
    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    registerResponse.EnsureSuccessStatusCode();
    var link = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var query = QueryHelpers.ParseQuery(link.Query);
    var userId = query["userId"].ToString();
    var code = query["code"].ToString();
    using var firstConfirmation = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId, code },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, firstConfirmation.StatusCode);

    using var reusedResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId, code },
      cancellation.Token);
    using var invalidResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId, code = "not+base64" },
      cancellation.Token);
    using var nonexistentResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId = Guid.NewGuid(), code },
      cancellation.Token);

    var reusedProblem = await ReadProblemAsync(reusedResponse, cancellation.Token);
    var invalidProblem = await ReadProblemAsync(invalidResponse, cancellation.Token);
    var nonexistentProblem = await ReadProblemAsync(nonexistentResponse, cancellation.Token);
    Assert.Equal(HttpStatusCode.BadRequest, reusedResponse.StatusCode);
    Assert.Equal(reusedProblem, invalidProblem);
    Assert.Equal(reusedProblem, nonexistentProblem);
  }

  [Fact]
  public async Task ConfirmEmail_ExpiredToken_ReturnsControlledInvalidConfirmationProblem()
  {
    await using var factory = new AccountApiFactory(
      database.ConnectionString,
      confirmationTokenLifespan: TimeSpan.Zero);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("expired");
    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
    var link = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var query = QueryHelpers.ParseQuery(link.Query);
    var userId = query["userId"].ToString();
    var code = query["code"].ToString();
    await Task.Delay(TimeSpan.FromMilliseconds(20), cancellation.Token);

    using var expiredResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId, code },
      cancellation.Token);
    using var invalidResponse = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new { userId, code = "not+base64" },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.BadRequest, expiredResponse.StatusCode);
    Assert.Equal(
      await ReadProblemAsync(invalidResponse, cancellation.Token),
      await ReadProblemAsync(expiredResponse, cancellation.Token));
  }

  [Fact]
  public async Task Register_ClientIpLimitExceeded_Returns429ProblemDetails()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AccountRateLimits:RegisterIpPermitLimit"] = "2",
      ["AccountRateLimits:RegisterEmailPermitLimit"] = "100",
      ["AccountRateLimits:Window"] = "00:05:00"
    };
    await using var factory = new AccountApiFactory(database.ConnectionString, settings);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    using var firstResponse = await RegisterAsync(
      client,
      UniqueEmail("ip-one"),
      ValidPassword,
      cancellation.Token);
    using var secondResponse = await RegisterAsync(
      client,
      UniqueEmail("ip-two"),
      ValidPassword,
      cancellation.Token);
    using var limitedResponse = await RegisterAsync(
      client,
      UniqueEmail("ip-three"),
      ValidPassword,
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);
    await AssertRateLimitProblemAsync(limitedResponse, cancellation.Token);
  }

  [Fact]
  public async Task Register_NormalizedEmailLimitExceeded_Returns429ProblemDetails()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AccountRateLimits:RegisterIpPermitLimit"] = "100",
      ["AccountRateLimits:RegisterEmailPermitLimit"] = "2",
      ["AccountRateLimits:Window"] = "00:05:00"
    };
    await using var factory = new AccountApiFactory(database.ConnectionString, settings);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("email-limit");

    using var firstResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    using var secondResponse = await RegisterAsync(
      client,
      $"  {email.ToUpperInvariant()}  ",
      ValidPassword,
      cancellation.Token);
    using var limitedResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);
    await AssertRateLimitProblemAsync(limitedResponse, cancellation.Token);
    Assert.Single(factory.EmailSender.Messages);
  }

  [Fact]
  public async Task ResendConfirmation_ClientIpLimitIsIndependent_Returns429ProblemDetails()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AccountRateLimits:RegisterIpPermitLimit"] = "1",
      ["AccountRateLimits:RegisterEmailPermitLimit"] = "100",
      ["AccountRateLimits:ResendConfirmationIpPermitLimit"] = "1",
      ["AccountRateLimits:ResendConfirmationEmailPermitLimit"] = "100",
      ["AccountRateLimits:Window"] = "00:05:00"
    };
    await using var factory = new AccountApiFactory(database.ConnectionString, settings);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("resend-ip-limit");
    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);

    using var firstResendResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email },
      cancellation.Token);
    using var limitedResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email = UniqueEmail("resend-ip-other") },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, firstResendResponse.StatusCode);
    await AssertRateLimitProblemAsync(limitedResponse, cancellation.Token);
  }

  [Fact]
  public async Task ResendConfirmation_NormalizedEmailLimitExceeded_Returns429ProblemDetails()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AccountRateLimits:RegisterIpPermitLimit"] = "100",
      ["AccountRateLimits:RegisterEmailPermitLimit"] = "100",
      ["AccountRateLimits:ResendConfirmationIpPermitLimit"] = "100",
      ["AccountRateLimits:ResendConfirmationEmailPermitLimit"] = "1",
      ["AccountRateLimits:Window"] = "00:05:00"
    };
    await using var factory = new AccountApiFactory(database.ConnectionString, settings);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("resend-email-limit");
    using var registerResponse = await RegisterAsync(
      client,
      email,
      ValidPassword,
      cancellation.Token);
    Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
    factory.EmailSender.Clear();

    using var firstResendResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email = $"  {email.ToUpperInvariant()}  " },
      cancellation.Token);
    using var limitedResponse = await client.PostAsJsonAsync(
      "/auth/resend-confirmation",
      new { email },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, firstResendResponse.StatusCode);
    await AssertRateLimitProblemAsync(limitedResponse, cancellation.Token);
    Assert.Single(factory.EmailSender.Messages);
  }

  private static Task<HttpResponseMessage> RegisterAsync(
    HttpClient client,
    string email,
    string password,
    CancellationToken cancellationToken) => client.PostAsJsonAsync(
      "/auth/register",
      new { email, password },
      cancellationToken);

  private static async Task AssertRateLimitProblemAsync(
    HttpResponseMessage response,
    CancellationToken cancellationToken)
  {
    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    using var problem = await JsonDocument.ParseAsync(
      await response.Content.ReadAsStreamAsync(cancellationToken),
      cancellationToken: cancellationToken);
    Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
    Assert.Equal("Too many account requests.", problem.RootElement.GetProperty("title").GetString());
  }

  private static async Task<string> ReadProblemAsync(
    HttpResponseMessage response,
    CancellationToken cancellationToken)
  {
    using var problem = await JsonDocument.ParseAsync(
      await response.Content.ReadAsStreamAsync(cancellationToken),
      cancellationToken: cancellationToken);
    return string.Join(
      '|',
      problem.RootElement.GetProperty("status").GetInt32(),
      problem.RootElement.GetProperty("title").GetString(),
      problem.RootElement.GetProperty("detail").GetString(),
      problem.RootElement.GetProperty("type").GetString());
  }

  private static Uri ExtractConfirmationUri(string htmlBody)
  {
    var match = ConfirmationLinkRegex().Match(htmlBody);
    Assert.True(match.Success, "The confirmation email must contain one link.");
    return new Uri(WebUtility.HtmlDecode(match.Groups[1].Value), UriKind.Absolute);
  }

  private static string UniqueEmail(string prefix) =>
    $"{prefix}-{Guid.NewGuid():N}@example.test";

  [GeneratedRegex("href=\"([^\"]+)\"")]
  private static partial Regex ConfirmationLinkRegex();
}
