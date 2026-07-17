namespace AstronomyExplorer.Api.Auth.Dtos;

public sealed record RegisterRequest(string? Email, string? Password);

public sealed record ResendConfirmationRequest(string? Email);

public sealed record ConfirmEmailRequest(Guid UserId, string? Code);

public sealed record AccountRequestAcceptedResponse(string Message);
