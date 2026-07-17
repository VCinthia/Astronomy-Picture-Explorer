using AstronomyExplorer.Api.Data;
using AstronomyExplorer.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddDataProtection();

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

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
