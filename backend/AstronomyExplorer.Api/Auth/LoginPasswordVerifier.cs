using AstronomyExplorer.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace AstronomyExplorer.Api.Auth;

public sealed class LoginPasswordVerifier(IPasswordHasher<ApplicationUser> passwordHasher)
{
  private const string DummyPassword = "Dummy1!Password-Not-A-Credential";
  private readonly ApplicationUser _dummyUser = new()
  {
    Id = Guid.Empty,
    Email = "dummy@example.invalid",
    UserName = "dummy@example.invalid"
  };
  private readonly string _dummyPasswordHash = passwordHasher.HashPassword(
    new ApplicationUser { Id = Guid.Empty },
    DummyPassword);

  public bool Verify(ApplicationUser? user, string? suppliedPassword)
  {
    var passwordHash = user?.PasswordHash ?? _dummyPasswordHash;
    var verificationUser = user ?? _dummyUser;
    var verification = passwordHasher.VerifyHashedPassword(
      verificationUser,
      passwordHash,
      suppliedPassword ?? string.Empty);

    return user is not null &&
      user.PasswordHash is not null &&
      !string.IsNullOrEmpty(suppliedPassword) &&
      verification != PasswordVerificationResult.Failed;
  }
}
