using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstronomyExplorer.Api.Auth;
using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
  public async Task ForgotPassword_ConfirmedMissingUnconfirmedAndInvalidRequests_AreIndistinguishable()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var confirmedEmail = UniqueEmail("password-reset-confirmed");
    var unconfirmedEmail = UniqueEmail("password-reset-unconfirmed");

    using var confirmedRegistration = await RegisterAsync(
      client,
      confirmedEmail,
      ValidPassword,
      cancellation.Token);
    var confirmationUri = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var confirmationQuery = QueryHelpers.ParseQuery(confirmationUri.Query);
    using var confirmation = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new
      {
        userId = confirmationQuery["userId"].ToString(),
        code = confirmationQuery["code"].ToString()
      },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
    using var unconfirmedRegistration = await RegisterAsync(
      client,
      unconfirmedEmail,
      ValidPassword,
      cancellation.Token);
    factory.EmailSender.Clear();

    using var confirmed = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email = confirmedEmail },
      cancellation.Token);
    var confirmedBody = await confirmed.Content.ReadAsStringAsync(cancellation.Token);
    using var missing = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email = UniqueEmail("password-reset-missing") },
      cancellation.Token);
    using var unconfirmed = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email = unconfirmedEmail },
      cancellation.Token);
    using var invalid = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email = string.Empty },
      cancellation.Token);

    Assert.All(
      new[] { confirmed, missing, unconfirmed, invalid },
      response =>
      {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
      });
    Assert.Equal(confirmedBody, await missing.Content.ReadAsStringAsync(cancellation.Token));
    Assert.Equal(confirmedBody, await unconfirmed.Content.ReadAsStringAsync(cancellation.Token));
    Assert.Equal(confirmedBody, await invalid.Content.ReadAsStringAsync(cancellation.Token));

    var resetEmail = Assert.Single(factory.EmailSender.Messages);
    Assert.Equal(confirmedEmail, resetEmail.Recipient);
    var resetUri = ExtractPasswordResetUri(resetEmail.HtmlBody);
    var resetQuery = QueryHelpers.ParseQuery(resetUri.Query);
    Assert.Equal("/reset-password", resetUri.AbsolutePath);
    Assert.Matches("^[A-Za-z0-9_-]+$", resetQuery["code"].ToString());
  }

  [Fact]
  public async Task ResetPassword_ValidOneShotToken_RevokesEveryRefreshSessionAndExpiresSuppliedCookie()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient(new() { HandleCookies = false });
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("reset-sessions");
    using var registration = await RegisterAsync(client, email, ValidPassword, cancellation.Token);
    var confirmationUri = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var confirmationQuery = QueryHelpers.ParseQuery(confirmationUri.Query);
    using var confirmation = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new
      {
        userId = confirmationQuery["userId"].ToString(),
        code = confirmationQuery["code"].ToString()
      },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
    factory.EmailSender.Clear();
    using var staleLoginScope = factory.Services.CreateScope();
    var staleUser = await staleLoginScope.ServiceProvider
      .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AstronomyExplorer.Api.Domain.ApplicationUser>>()
      .FindByEmailAsync(email);
    Assert.NotNull(staleUser);

    using var firstLogin = await client.PostAsJsonAsync(
      "/auth/login",
      new { email, password = ValidPassword },
      cancellation.Token);
    using var secondLogin = await client.PostAsJsonAsync(
      "/auth/login",
      new { email, password = ValidPassword },
      cancellation.Token);
    var firstToken = ExtractCookieValue(Assert.Single(firstLogin.Headers.GetValues("Set-Cookie")));
    var secondToken = ExtractCookieValue(Assert.Single(secondLogin.Headers.GetValues("Set-Cookie")));
    Assert.NotEqual(firstToken, secondToken);

    using var forgot = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
    var resetUri = ExtractPasswordResetUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var resetQuery = QueryHelpers.ParseQuery(resetUri.Query);
    using var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/reset-password")
    {
      Content = JsonContent.Create(new
      {
        userId = resetQuery["userId"].ToString(),
        code = resetQuery["code"].ToString(),
        password = "New2!Password"
      })
    };
    resetRequest.Headers.Add("Cookie", $"ape.refresh={firstToken}");
    using var reset = await client.SendAsync(resetRequest, cancellation.Token);

    Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    Assert.Contains("no-store", reset.Headers.CacheControl?.ToString());
    Assert.Contains(
      "max-age=0",
      Assert.Single(reset.Headers.GetValues("Set-Cookie")),
      StringComparison.OrdinalIgnoreCase);
    await using (var context = database.CreateDbContext())
    {
      Assert.False(await context.RefreshSessions.AnyAsync(
        session => session.User.Email == email && session.RevokedAt == null,
        cancellation.Token));
    }
    var staleSession = await staleLoginScope.ServiceProvider
      .GetRequiredService<RefreshSessionService>()
      .CreateAsync(staleUser!, cancellation.Token);
    Assert.Null(staleSession);

    using var oldPassword = await client.PostAsJsonAsync(
      "/auth/login",
      new { email, password = ValidPassword },
      cancellation.Token);
    using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
    refreshRequest.Headers.Add("Origin", "https://portfolio.example");
    refreshRequest.Headers.Add("Cookie", $"ape.refresh={secondToken}");
    using var refresh = await client.SendAsync(refreshRequest, cancellation.Token);
    using var newPassword = await client.PostAsJsonAsync(
      "/auth/login",
      new { email, password = "New2!Password" },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
  }

  [Fact]
  public async Task ResetPassword_RotationAlreadyInFlight_CannotLeaveAnActiveReplacementSession()
  {
    var userSessionLock = new GatedUserSessionLock();
    await using var factory = new AccountApiFactory(
      database.ConnectionString,
      userSessionLock: userSessionLock);
    using var client = factory.CreateClient(new() { HandleCookies = false });
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("reset-rotation-race");
    using var registration = await RegisterAsync(client, email, ValidPassword, cancellation.Token);
    var confirmationUri = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var confirmationQuery = QueryHelpers.ParseQuery(confirmationUri.Query);
    using var confirmation = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new
      {
        userId = confirmationQuery["userId"].ToString(),
        code = confirmationQuery["code"].ToString()
      },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
    factory.EmailSender.Clear();

    using var login = await client.PostAsJsonAsync(
      "/auth/login",
      new { email, password = ValidPassword },
      cancellation.Token);
    var refreshToken = ExtractCookieValue(Assert.Single(login.Headers.GetValues("Set-Cookie")));
    using var forgot = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email },
      cancellation.Token);
    var resetUri = ExtractPasswordResetUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var resetQuery = QueryHelpers.ParseQuery(resetUri.Query);

    userSessionLock.Arm();
    using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
    refreshRequest.Headers.Add("Origin", "https://portfolio.example");
    refreshRequest.Headers.Add("Cookie", $"ape.refresh={refreshToken}");
    var refreshTask = client.SendAsync(refreshRequest, cancellation.Token);
    await userSessionLock.WaitForFirstAcquisitionAsync().WaitAsync(
      TimeSpan.FromSeconds(5),
      cancellation.Token);

    using var reset = await client.PostAsJsonAsync(
      "/auth/reset-password",
      new
      {
        userId = resetQuery["userId"].ToString(),
        code = resetQuery["code"].ToString(),
        password = "New2!Password"
      },
      cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    userSessionLock.ReleaseFirstAcquisition();
    using var refresh = await refreshTask;

    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    await using var context = database.CreateDbContext();
    Assert.False(await context.RefreshSessions.AnyAsync(
      session => session.User.Email == email && session.RevokedAt == null,
      cancellation.Token));
  }

  [Fact]
  public async Task ResetPassword_InvalidReusedMissingAndPasswordFailure_ReturnSameControlledProblem()
  {
    await using var factory = new AccountApiFactory(database.ConnectionString);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var email = UniqueEmail("reset-invalid");
    using var registration = await RegisterAsync(client, email, ValidPassword, cancellation.Token);
    var confirmationUri = ExtractConfirmationUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var confirmationQuery = QueryHelpers.ParseQuery(confirmationUri.Query);
    using var confirmation = await client.PostAsJsonAsync(
      "/auth/confirm-email",
      new
      {
        userId = confirmationQuery["userId"].ToString(),
        code = confirmationQuery["code"].ToString()
      },
      cancellation.Token);
    factory.EmailSender.Clear();
    using var forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email }, cancellation.Token);
    var resetUri = ExtractPasswordResetUri(Assert.Single(factory.EmailSender.Messages).HtmlBody);
    var resetQuery = QueryHelpers.ParseQuery(resetUri.Query);
    var validRequest = new
    {
      userId = resetQuery["userId"].ToString(),
      code = resetQuery["code"].ToString(),
      password = "New2!Password"
    };
    using var weakPassword = await client.PostAsJsonAsync(
      "/auth/reset-password",
      new { validRequest.userId, validRequest.code, password = "short" },
      cancellation.Token);
    using var success = await client.PostAsJsonAsync("/auth/reset-password", validRequest, cancellation.Token);
    Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);

    using var reused = await client.PostAsJsonAsync("/auth/reset-password", validRequest, cancellation.Token);
    using var invalid = await client.PostAsJsonAsync(
      "/auth/reset-password",
      new { userId = "not-a-guid", code = "not+base64", password = "New2!Password" },
      cancellation.Token);
    using var missing = await client.PostAsJsonAsync(
      "/auth/reset-password",
      new { userId = Guid.NewGuid().ToString(), code = validRequest.code, password = "New2!Password" },
      cancellation.Token);

    var reusedProblem = await ReadProblemAsync(reused, cancellation.Token);
    Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    Assert.Equal(reusedProblem, await ReadProblemAsync(weakPassword, cancellation.Token));
    Assert.Equal(reusedProblem, await ReadProblemAsync(invalid, cancellation.Token));
    Assert.Equal(reusedProblem, await ReadProblemAsync(missing, cancellation.Token));
  }

  [Fact]
  public async Task ForgotAndResetPassword_IpLimits_ReturnNoStoreProblemDetails()
  {
    var settings = new Dictionary<string, string?>
    {
      ["AccountRateLimits:ForgotPasswordIpPermitLimit"] = "1",
      ["AccountRateLimits:ForgotPasswordEmailPermitLimit"] = "100",
      ["AccountRateLimits:ResetPasswordIpPermitLimit"] = "1"
    };
    await using var factory = new AccountApiFactory(database.ConnectionString, settings);
    using var client = factory.CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    using var forgot = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email = UniqueEmail("forgot-limit") },
      cancellation.Token);
    using var forgotLimited = await client.PostAsJsonAsync(
      "/auth/forgot-password",
      new { email = UniqueEmail("forgot-limit-other") },
      cancellation.Token);
    using var reset = await client.PostAsJsonAsync(
      "/auth/reset-password",
      new { userId = "not-a-guid", code = "invalid", password = "New2!Password" },
      cancellation.Token);
    using var resetLimited = await client.PostAsJsonAsync(
      "/auth/reset-password",
      new { userId = "not-a-guid", code = "invalid", password = "New2!Password" },
      cancellation.Token);

    Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    await AssertRateLimitProblemAsync(forgotLimited, cancellation.Token);
    await AssertRateLimitProblemAsync(resetLimited, cancellation.Token);
    Assert.Contains("no-store", forgotLimited.Headers.CacheControl?.ToString());
    Assert.Contains("no-store", resetLimited.Headers.CacheControl?.ToString());
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

  private static Uri ExtractPasswordResetUri(string htmlBody)
  {
    var match = ConfirmationLinkRegex().Match(htmlBody);
    Assert.True(match.Success, "The password reset email must contain one link.");
    return new Uri(WebUtility.HtmlDecode(match.Groups[1].Value), UriKind.Absolute);
  }

  private static string ExtractCookieValue(string setCookie)
  {
    var pair = setCookie.Split(';', 2)[0];
    return pair[(pair.IndexOf('=') + 1)..];
  }

  private static string UniqueEmail(string prefix) =>
    $"{prefix}-{Guid.NewGuid():N}@example.test";

  [GeneratedRegex("href=\"([^\"]+)\"")]
  private static partial Regex ConfirmationLinkRegex();
}
