using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Auth;
using AstronomyExplorer.Api.Email;
using AstronomyExplorer.Api.Security;
using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Nasa;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
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
builder.Services.AddSingleton<IValidateOptions<FrontendOptions>, FrontendOptionsValidator>();
builder.Services.AddOptions<FrontendOptions>()
  .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
  .ValidateOnStart();
builder.Services.Configure<ResendEmailOptions>(
  builder.Configuration.GetSection(ResendEmailOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<AuthSessionOptions>, SessionOptionsValidator>();
builder.Services.AddOptions<AuthSessionOptions>()
  .Bind(builder.Configuration.GetSection(AuthSessionOptions.SectionName))
  .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<NasaApodOptions>, NasaApodOptionsValidator>();
builder.Services.AddOptions<NasaApodOptions>()
  .Bind(builder.Configuration.GetSection(NasaApodOptions.SectionName))
  .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ApodCacheOptions>, ApodCacheOptionsValidator>();
builder.Services.AddOptions<ApodCacheOptions>()
  .Bind(builder.Configuration.GetSection(ApodCacheOptions.SectionName))
  .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IValidateOptions<CatalogOptions>, CatalogOptionsValidator>();
builder.Services.AddOptions<CatalogOptions>()
  .Bind(builder.Configuration.GetSection(CatalogOptions.SectionName))
  .ValidateOnStart();

var nasaApodOptions = builder.Configuration
  .GetSection(NasaApodOptions.SectionName)
  .Get<NasaApodOptions>() ?? new NasaApodOptions();
var apodCacheOptions = builder.Configuration
  .GetSection(ApodCacheOptions.SectionName)
  .Get<ApodCacheOptions>() ?? new ApodCacheOptions();
builder.Services.AddMemoryCache(options =>
  options.SizeLimit = apodCacheOptions.MaxEntries);

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
  options.AddPolicy(AccountRateLimitPolicies.LoginByIp, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      AccountRateLimitPartitionKeys.FromRemoteIp(httpContext),
      _ => AccountRateLimitPolicies.CreateFixedWindowOptions(
        accountRateLimitOptions.LoginIpPermitLimit,
        accountRateLimitOptions.Window)));
});

builder.Services.AddSingleton<IAccountEmailRateLimiter, AccountEmailRateLimiter>();
builder.Services.AddSingleton<EmailConfirmationLinkFactory>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RefreshSessionService>();
builder.Services.AddScoped<RefreshCookieService>();
builder.Services.AddScoped<LoginPasswordVerifier>();
builder.Services.AddSingleton<ApodSingleFlight>();
builder.Services.AddSingleton<ApodCacheService>();
builder.Services.AddHttpClient<INasaApodClient, NasaApodClient>(client =>
{
  client.BaseAddress = new Uri("https://api.nasa.gov/");
  client.DefaultRequestHeaders.UserAgent.ParseAdd("AstronomyExplorer/1.0");
  client.Timeout = nasaApodOptions.Timeout;
})
  .ConfigurePrimaryHttpMessageHandler(NasaApodHttpClientConfiguration.CreatePrimaryHandler)
  .RedactLoggedHeaders(["X-Api-Key"]);
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
  .Configure<IOptions<AuthSessionOptions>>((jwtOptions, sessionOptions) =>
  {
    jwtOptions.MapInboundClaims = false;
    jwtOptions.TokenValidationParameters =
      JwtTokenService.CreateValidationParameters(sessionOptions.Value);
  });
builder.Services.AddAuthorization();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgresql");

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapAccountEndpoints();
app.MapSessionEndpoints();
app.MapApodEndpoints();
app.MapCatalogStatusEndpoint();

app.Run();

public partial class Program;
