using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Auth;
using AstronomyExplorer.Api.Email;
using AstronomyExplorer.Api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
var hasPostgresConnection = !string.IsNullOrWhiteSpace(postgresConnection);
if (!hasPostgresConnection && !EF.IsDesignTime)
{
  throw new InvalidOperationException(
      "ConnectionStrings:Postgres must be provided through environment variables or user secrets.");
}

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDataProtection()
  .SetApplicationName("AstronomyExplorer")
  .PersistKeysToDbContext<AppDbContext>();
builder.Services.Configure<AccountRateLimitOptions>(
  builder.Configuration.GetSection(AccountRateLimitOptions.SectionName));
builder.Services.Configure<FrontendOptions>(
  builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.Configure<ResendEmailOptions>(
  builder.Configuration.GetSection(ResendEmailOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);

var accountRateLimitOptions = builder.Configuration
  .GetSection(AccountRateLimitOptions.SectionName)
  .Get<AccountRateLimitOptions>() ?? new AccountRateLimitOptions();

builder.Services.AddRateLimiter(options =>
{
  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
  options.OnRejected = AccountRateLimitProblemDetails.WriteAsync;
  options.AddPolicy(AccountRateLimitPolicies.RegisterByIp, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      AccountRateLimitPartitionKeys.FromRemoteIp(httpContext),
      _ => AccountRateLimitPolicies.CreateFixedWindowOptions(
        accountRateLimitOptions.RegisterIpPermitLimit,
        accountRateLimitOptions.Window)));
  options.AddPolicy(AccountRateLimitPolicies.ResendConfirmationByIp, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      AccountRateLimitPartitionKeys.FromRemoteIp(httpContext),
      _ => AccountRateLimitPolicies.CreateFixedWindowOptions(
        accountRateLimitOptions.ResendConfirmationIpPermitLimit,
        accountRateLimitOptions.Window)));
});

builder.Services.AddSingleton<IAccountEmailRateLimiter, AccountEmailRateLimiter>();
builder.Services.AddSingleton<EmailConfirmationLinkFactory>();
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
{
  client.BaseAddress = new Uri("https://api.resend.com/");
  client.DefaultRequestHeaders.UserAgent.ParseAdd("AstronomyExplorer/1.0");
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
  if (hasPostgresConnection)
  {
    options.UseNpgsql(postgresConnection);
  }
  else
  {
    options.UseNpgsql();
  }
});

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
        options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgresql");

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapAccountEndpoints();

app.Run();

public partial class Program;
