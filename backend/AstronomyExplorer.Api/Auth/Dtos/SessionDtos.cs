namespace AstronomyExplorer.Api.Auth.Dtos;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record SessionUserResponse(Guid Id, string Email);

public sealed record SessionResponse(
  string AccessToken,
  DateTimeOffset ExpiresAt,
  SessionUserResponse User);
