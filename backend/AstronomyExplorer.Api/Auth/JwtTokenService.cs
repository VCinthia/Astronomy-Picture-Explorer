using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AstronomyExplorer.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AstronomyExplorer.Api.Auth;

public sealed class JwtTokenService(
  IOptions<AuthSessionOptions> options,
  TimeProvider timeProvider)
{
  private readonly AuthSessionOptions _options = options.Value;

  public AccessTokenResult Create(ApplicationUser user)
  {
    var issuedAt = timeProvider.GetUtcNow();
    var expiresAt = issuedAt.Add(_options.AccessTokenLifetime);
    var credentials = new SigningCredentials(
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
      SecurityAlgorithms.HmacSha256);
    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new Claim(JwtRegisteredClaimNames.Email, user.Email!),
      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
      new Claim(JwtRegisteredClaimNames.Iat,
        EpochTime.GetIntDate(issuedAt.UtcDateTime).ToString(),
        ClaimValueTypes.Integer64),
      new Claim("client_id", _options.ClientId)
    };
    var token = new JwtSecurityToken(
      _options.Issuer,
      _options.Audience,
      claims,
      issuedAt.UtcDateTime,
      expiresAt.UtcDateTime,
      credentials);

    return new AccessTokenResult(
      new JwtSecurityTokenHandler().WriteToken(token),
      expiresAt);
  }

  public static TokenValidationParameters CreateValidationParameters(AuthSessionOptions options) => new()
  {
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
    ValidateIssuer = true,
    ValidIssuer = options.Issuer,
    ValidateAudience = true,
    ValidAudience = options.Audience,
    ValidateLifetime = true,
    RequireExpirationTime = true,
    RequireSignedTokens = true,
    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
    ClockSkew = TimeSpan.Zero
  };
}

public sealed record AccessTokenResult(string Value, DateTimeOffset ExpiresAt);
