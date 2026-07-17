using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AstronomyExplorer.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseSchemaTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task InitialMigration_CreatesIdentityAndApplicationSchema()
  {
    await using var context = database.CreateDbContext();

    var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate"));

    await using var connection = new NpgsqlConnection(database.ConnectionString);
    await connection.OpenAsync();

    const string tableSql =
        """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public';
            """;

    await using var tableCommand = new NpgsqlCommand(tableSql, connection);
    await using var reader = await tableCommand.ExecuteReaderAsync();
    var tableNames = new HashSet<string>(StringComparer.Ordinal);

    while (await reader.ReadAsync())
    {
      tableNames.Add(reader.GetString(0));
    }

    var expectedTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "AspNetUsers",
            "AspNetUserClaims",
            "AspNetUserLogins",
            "AspNetUserTokens",
            "refresh_sessions",
            "apod_entries",
            "favorites",
            "catalog_sync_state"
        };

    foreach (var expectedTable in expectedTables)
    {
      Assert.Contains(expectedTable, tableNames);
    }

    Assert.DoesNotContain("AspNetRoles", tableNames);
    Assert.DoesNotContain("AspNetUserRoles", tableNames);
  }

  [Fact]
  public async Task ApodEntry_ComputesWeightedSearchVectorAndUsesGinIndex()
  {
    var date = new DateOnly(2026, 7, 17);

    await using (var context = database.CreateDbContext())
    {
      context.ApodEntries.Add(new ApodEntry
      {
        Date = date,
        Title = "Quasar Beacon",
        Explanation = "A distant nebula surrounds the beacon.",
        MediaType = "image",
        Url = "https://example.test/apod.jpg",
        CachedAt = DateTimeOffset.UtcNow
      });

      await context.SaveChangesAsync();
    }

    await using var connection = new NpgsqlConnection(database.ConnectionString);
    await connection.OpenAsync();

    const string vectorSql =
        "SELECT search_vector::text FROM apod_entries WHERE date = @date;";
    await using var vectorCommand = new NpgsqlCommand(vectorSql, connection);
    vectorCommand.Parameters.AddWithValue("date", date);
    var vector = Assert.IsType<string>(await vectorCommand.ExecuteScalarAsync());

    Assert.Contains("'quasar':1A", vector, StringComparison.Ordinal);
    Assert.Contains("'nebula':5B", vector, StringComparison.Ordinal);

    const string indexSql =
        """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'apod_entries'
              AND indexname = 'ix_apod_entries_search_vector';
            """;
    await using var indexCommand = new NpgsqlCommand(indexSql, connection);
    var indexDefinition = Assert.IsType<string>(await indexCommand.ExecuteScalarAsync());

    Assert.Contains("USING gin", indexDefinition, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Constraints_RejectInvalidIdentitySessionFavoriteAndCatalogRows()
  {
    var userId = Guid.NewGuid();
    var apodDate = new DateOnly(2026, 7, 16);
    var replacementId = Guid.NewGuid();

    await using (var context = database.CreateDbContext())
    {
      context.Users.Add(new ApplicationUser
      {
        Id = userId,
        UserName = "schema-user@example.test",
        NormalizedUserName = "SCHEMA-USER@EXAMPLE.TEST",
        Email = "schema-user@example.test",
        NormalizedEmail = "SCHEMA-USER@EXAMPLE.TEST"
      });
      context.ApodEntries.Add(new ApodEntry
      {
        Date = apodDate,
        Title = "Constraint fixture",
        Explanation = "A valid APOD row for relational constraints.",
        MediaType = "video",
        Url = "https://example.test/apod",
        ThumbnailUrl = null,
        CachedAt = DateTimeOffset.UtcNow
      });
      context.RefreshSessions.AddRange(
          new RefreshSession
          {
            Id = replacementId,
            UserId = userId,
            TokenHash = "replacement-token-hash",
            FamilyId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
          },
          new RefreshSession
          {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "original-token-hash",
            FamilyId = Guid.NewGuid(),
            ReplacedByTokenId = replacementId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
          });
      context.Favorites.Add(new Favorite
      {
        UserId = userId,
        ApodDate = apodDate,
        CreatedAt = DateTimeOffset.UtcNow
      });
      context.CatalogSyncStates.Add(new CatalogSyncState
      {
        Id = Guid.NewGuid(),
        TargetFrom = new DateOnly(2026, 1, 1),
        TargetTo = new DateOnly(2026, 1, 31),
        Status = CatalogSyncStatus.Running,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
      });

      await context.SaveChangesAsync();
    }

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO "AspNetUsers"
                ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                 "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled",
                 "LockoutEnabled", "AccessFailedCount")
            VALUES
                (@id, 'duplicate@example.test', 'DUPLICATE@EXAMPLE.TEST',
                 'duplicate@example.test', 'SCHEMA-USER@EXAMPLE.TEST',
                 FALSE, FALSE, FALSE, FALSE, 0);
            """,
        PostgresErrorCodes.UniqueViolation,
        new NpgsqlParameter("id", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO refresh_sessions
                (id, user_id, token_hash, family_id, replaced_by_token_id, created_at, expires_at)
            VALUES
                (@id, @userId, 'another-original-hash', @familyId, @replacementId,
                 CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '7 days');
            """,
        PostgresErrorCodes.UniqueViolation,
        new NpgsqlParameter("id", Guid.NewGuid()),
        new NpgsqlParameter("userId", userId),
        new NpgsqlParameter("familyId", Guid.NewGuid()),
        new NpgsqlParameter("replacementId", replacementId));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO refresh_sessions
                (id, user_id, token_hash, family_id, created_at, expires_at)
            VALUES
                (@id, @userId, 'original-token-hash', @familyId,
                 CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '7 days');
            """,
        PostgresErrorCodes.UniqueViolation,
        new NpgsqlParameter("id", Guid.NewGuid()),
        new NpgsqlParameter("userId", userId),
        new NpgsqlParameter("familyId", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO refresh_sessions
                (id, user_id, token_hash, family_id, created_at, expires_at)
            VALUES
                (@id, @userId, 'expired-before-created', @familyId,
                 CURRENT_TIMESTAMP, CURRENT_TIMESTAMP - INTERVAL '1 minute');
            """,
        PostgresErrorCodes.CheckViolation,
        new NpgsqlParameter("id", Guid.NewGuid()),
        new NpgsqlParameter("userId", userId),
        new NpgsqlParameter("familyId", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO favorites (user_id, apod_date)
            VALUES (@userId, @apodDate);
            """,
        PostgresErrorCodes.UniqueViolation,
        new NpgsqlParameter("userId", userId),
        new NpgsqlParameter("apodDate", apodDate));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO catalog_sync_state
                (id, target_from, target_to, status)
            VALUES
                (@id, DATE '2026-02-02', DATE '2026-02-01', 'Pending');
            """,
        PostgresErrorCodes.CheckViolation,
        new NpgsqlParameter("id", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO catalog_sync_state
                (id, target_from, target_to, status, created_at, updated_at)
            VALUES
                (@id, DATE '2026-04-01', DATE '2026-04-30', 'Pending',
                 TIMESTAMPTZ '2026-04-02 00:00:00+00', TIMESTAMPTZ '2026-04-01 00:00:00+00');
            """,
        PostgresErrorCodes.CheckViolation,
        new NpgsqlParameter("id", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO catalog_sync_state
                (id, target_from, target_to, last_completed_date, status)
            VALUES
                (@id, DATE '2026-02-01', DATE '2026-02-28', DATE '2026-03-01', 'Paused');
            """,
        PostgresErrorCodes.CheckViolation,
        new NpgsqlParameter("id", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO catalog_sync_state
                (id, target_from, target_to, status)
            VALUES
                (@id, DATE '2026-03-01', DATE '2026-03-31', 'Unknown');
            """,
        PostgresErrorCodes.CheckViolation,
        new NpgsqlParameter("id", Guid.NewGuid()));

    await AssertDatabaseErrorAsync(
        """
            INSERT INTO apod_entries
                (date, title, explanation, media_type, url)
            VALUES
                (DATE '2026-07-15', 'Invalid media', 'Invalid media fixture', 'audio',
                 'https://example.test/audio');
            """,
        PostgresErrorCodes.CheckViolation);
  }

  private async Task AssertDatabaseErrorAsync(
      string commandText,
      string expectedSqlState,
      params NpgsqlParameter[] parameters)
  {
    await using var connection = new NpgsqlConnection(database.ConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(commandText, connection);
    command.Parameters.AddRange(parameters);

    var exception = await Assert.ThrowsAsync<PostgresException>(
        () => command.ExecuteNonQueryAsync());

    Assert.Equal(expectedSqlState, exception.SqlState);
  }
}
