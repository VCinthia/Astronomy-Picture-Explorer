using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AstronomyExplorer.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseRelationshipTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task FavoriteForeignKeys_RejectMissingUserAndApodEntry()
  {
    var userId = Guid.NewGuid();
    var apodDate = new DateOnly(2026, 6, 1);

    await using (var context = database.CreateDbContext())
    {
      context.Users.Add(CreateUser(userId, "favorite-fk@example.test"));
      context.ApodEntries.Add(CreateApodEntry(apodDate));
      await context.SaveChangesAsync();
    }

    await PostgresAssert.ThrowsSqlStateAsync(
      database.ConnectionString,
      "INSERT INTO favorites (user_id, apod_date) VALUES (@userId, @apodDate);",
      PostgresErrorCodes.ForeignKeyViolation,
      new NpgsqlParameter("userId", Guid.NewGuid()),
      new NpgsqlParameter("apodDate", apodDate));

    await PostgresAssert.ThrowsSqlStateAsync(
      database.ConnectionString,
      "INSERT INTO favorites (user_id, apod_date) VALUES (@userId, @apodDate);",
      PostgresErrorCodes.ForeignKeyViolation,
      new NpgsqlParameter("userId", userId),
      new NpgsqlParameter("apodDate", new DateOnly(1998, 1, 1)));
  }

  [Fact]
  public async Task DeleteBehaviors_RestrictApodAndCascadeUserGraph()
  {
    var userId = Guid.NewGuid();
    var apodDate = new DateOnly(2026, 6, 2);
    var familyId = Guid.NewGuid();
    var replacementId = Guid.NewGuid();
    var createdAt = DateTimeOffset.UtcNow;

    await using (var context = database.CreateDbContext())
    {
      context.Users.Add(CreateUser(userId, "cascade@example.test"));
      context.ApodEntries.Add(CreateApodEntry(apodDate));
      context.Favorites.Add(new Favorite
      {
        UserId = userId,
        ApodDate = apodDate,
        CreatedAt = createdAt
      });
      context.RefreshSessions.AddRange(
        new RefreshSession
        {
          Id = replacementId,
          UserId = userId,
          TokenHash = $"replacement-{userId:N}",
          FamilyId = familyId,
          CreatedAt = createdAt,
          ExpiresAt = createdAt.AddDays(7)
        },
        new RefreshSession
        {
          Id = Guid.NewGuid(),
          UserId = userId,
          TokenHash = $"original-{userId:N}",
          FamilyId = familyId,
          ReplacedByTokenId = replacementId,
          CreatedAt = createdAt,
          ExpiresAt = createdAt.AddDays(7)
        });

      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateDbContext())
    {
      var apodEntry = await context.ApodEntries.SingleAsync(entry => entry.Date == apodDate);
      context.ApodEntries.Remove(apodEntry);

      var exception = await Assert.ThrowsAsync<DbUpdateException>(
        () => context.SaveChangesAsync());

      var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
      Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
    }

    await using (var context = database.CreateDbContext())
    {
      var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
      context.Users.Remove(user);
      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateDbContext())
    {
      Assert.False(await context.Users.AnyAsync(candidate => candidate.Id == userId));
      Assert.False(await context.Favorites.AnyAsync(favorite => favorite.UserId == userId));
      Assert.False(await context.RefreshSessions.AnyAsync(session => session.UserId == userId));
      Assert.True(await context.ApodEntries.AnyAsync(entry => entry.Date == apodDate));
    }
  }

  private static ApplicationUser CreateUser(Guid id, string email)
  {
    return new ApplicationUser
    {
      Id = id,
      UserName = email,
      NormalizedUserName = email.ToUpperInvariant(),
      Email = email,
      NormalizedEmail = email.ToUpperInvariant()
    };
  }

  private static ApodEntry CreateApodEntry(DateOnly date)
  {
    return new ApodEntry
    {
      Date = date,
      Title = "Relationship fixture",
      Explanation = "An APOD row used to verify foreign keys.",
      MediaType = "image",
      Url = "https://example.test/relationship.jpg",
      CachedAt = DateTimeOffset.UtcNow
    };
  }
}
