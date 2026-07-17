using AstronomyExplorer.Api.Domain;
using AstronomyExplorer.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AstronomyExplorer.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ApodSchemaTests(PostgreSqlFixture database)
{
  [Fact]
  public async Task RequiredApodFields_RejectNullValues()
  {
    var invalidRows = new[]
    {
      "(DATE '2026-05-01', NULL, 'Explanation', 'image', 'https://example.test/1')",
      "(DATE '2026-05-02', 'Title', NULL, 'image', 'https://example.test/2')",
      "(DATE '2026-05-03', 'Title', 'Explanation', NULL, 'https://example.test/3')",
      "(DATE '2026-05-04', 'Title', 'Explanation', 'image', NULL)"
    };

    foreach (var invalidRow in invalidRows)
    {
      await PostgresAssert.ThrowsSqlStateAsync(
        database.ConnectionString,
        $"""
        INSERT INTO apod_entries (date, title, explanation, media_type, url)
        VALUES {invalidRow};
        """,
        PostgresErrorCodes.NotNullViolation);
    }
  }

  [Fact]
  public async Task OptionalApodFieldsAndUtcInstants_RoundTripWithoutDataLoss()
  {
    var userId = Guid.NewGuid();
    var date = new DateOnly(2026, 5, 5);
    var cachedAt = new DateTimeOffset(2026, 5, 5, 10, 15, 0, TimeSpan.Zero);
    var favoriteCreatedAt = new DateTimeOffset(2026, 5, 5, 11, 30, 0, TimeSpan.Zero);

    await using (var context = database.CreateDbContext())
    {
      var email = $"utc-{userId:N}@example.test";
      context.Users.Add(new ApplicationUser
      {
        Id = userId,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant()
      });
      context.ApodEntries.Add(new ApodEntry
      {
        Date = date,
        Title = "Nullable APOD fields",
        Explanation = "Optional provider fields are absent.",
        MediaType = "video",
        Url = "https://example.test/video",
        HdUrl = null,
        ThumbnailUrl = null,
        Copyright = null,
        CachedAt = cachedAt
      });
      context.Favorites.Add(new Favorite
      {
        UserId = userId,
        ApodDate = date,
        CreatedAt = favoriteCreatedAt
      });

      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateDbContext())
    {
      var apodEntry = await context.ApodEntries
        .AsNoTracking()
        .SingleAsync(entry => entry.Date == date);
      var favorite = await context.Favorites
        .AsNoTracking()
        .SingleAsync(entry => entry.UserId == userId && entry.ApodDate == date);

      Assert.Null(apodEntry.HdUrl);
      Assert.Null(apodEntry.ThumbnailUrl);
      Assert.Null(apodEntry.Copyright);
      Assert.Equal(TimeSpan.Zero, apodEntry.CachedAt.Offset);
      Assert.Equal(cachedAt, apodEntry.CachedAt);
      Assert.Equal(TimeSpan.Zero, favorite.CreatedAt.Offset);
      Assert.Equal(favoriteCreatedAt, favorite.CreatedAt);
    }
  }

  [Fact]
  public async Task PhysicalColumnTypes_UseDateUtcInstantsAndStoredSearchVector()
  {
    await using var connection = new NpgsqlConnection(database.ConnectionString);
    await connection.OpenAsync();

    const string columnsSql =
      """
      SELECT table_name, column_name, data_type, udt_name, is_generated
      FROM information_schema.columns
      WHERE table_schema = 'public'
        AND table_name IN ('apod_entries', 'favorites', 'refresh_sessions', 'catalog_sync_state');
      """;
    await using var columnsCommand = new NpgsqlCommand(columnsSql, connection);
    await using var reader = await columnsCommand.ExecuteReaderAsync();
    var columns = new Dictionary<string, (string DataType, string UdtName, string IsGenerated)>();

    while (await reader.ReadAsync())
    {
      columns[$"{reader.GetString(0)}.{reader.GetString(1)}"] =
        (reader.GetString(2), reader.GetString(3), reader.GetString(4));
    }

    var dateColumns = new[]
    {
      "apod_entries.date",
      "favorites.apod_date",
      "catalog_sync_state.target_from",
      "catalog_sync_state.target_to",
      "catalog_sync_state.last_completed_date"
    };
    foreach (var column in dateColumns)
    {
      Assert.Equal("date", columns[column].DataType);
    }

    var instantColumns = new[]
    {
      "apod_entries.cached_at",
      "favorites.created_at",
      "refresh_sessions.created_at",
      "refresh_sessions.expires_at",
      "refresh_sessions.revoked_at",
      "catalog_sync_state.created_at",
      "catalog_sync_state.updated_at"
    };
    foreach (var column in instantColumns)
    {
      Assert.Equal("timestamp with time zone", columns[column].DataType);
    }

    Assert.Equal("tsvector", columns["apod_entries.search_vector"].UdtName);
    Assert.Equal("ALWAYS", columns["apod_entries.search_vector"].IsGenerated);

    await reader.CloseAsync();

    const string storedSql =
      """
      SELECT attribute.attgenerated
      FROM pg_attribute AS attribute
      INNER JOIN pg_class AS table_info ON table_info.oid = attribute.attrelid
      WHERE table_info.relname = 'apod_entries'
        AND attribute.attname = 'search_vector';
      """;
    await using var storedCommand = new NpgsqlCommand(storedSql, connection);
    var generatedStorage = Assert.IsType<char>(await storedCommand.ExecuteScalarAsync());

    Assert.Equal('s', generatedStorage);
  }
}
