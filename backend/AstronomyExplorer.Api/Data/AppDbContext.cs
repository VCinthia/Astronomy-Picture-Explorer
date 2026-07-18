using AstronomyExplorer.Api.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IDataProtectionKeyContext
{
  public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

  public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

  public DbSet<ApodEntry> ApodEntries => Set<ApodEntry>();

  public DbSet<Favorite> Favorites => Set<Favorite>();

  public DbSet<CatalogSyncState> CatalogSyncStates => Set<CatalogSyncState>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    ConfigureIdentity(builder);
    ConfigureRefreshSessions(builder);
    ConfigureApodEntries(builder);
    ConfigureFavorites(builder);
    ConfigureCatalogSyncStates(builder);
  }

  private static void ConfigureIdentity(ModelBuilder builder)
  {
    builder.Entity<ApplicationUser>(entity =>
    {
      entity.HasIndex(user => user.NormalizedEmail)
              .HasDatabaseName("EmailIndex")
              .IsUnique();
    });
  }

  private static void ConfigureRefreshSessions(ModelBuilder builder)
  {
    builder.Entity<RefreshSession>(entity =>
    {
      entity.ToTable("refresh_sessions", tableBuilder =>
      {
        tableBuilder.HasCheckConstraint(
          "ck_refresh_sessions_expiry",
          "expires_at > created_at");
        tableBuilder.HasCheckConstraint(
          "ck_refresh_sessions_revocation",
          "revoked_at IS NULL OR revoked_at >= created_at");
        tableBuilder.HasCheckConstraint(
          "ck_refresh_sessions_replacement",
          "replaced_by_token_id IS NULL OR replaced_by_token_id <> id");
      });

      entity.HasKey(session => session.Id)
              .HasName("pk_refresh_sessions");

      entity.Property(session => session.Id).HasColumnName("id");
      entity.Property(session => session.UserId).HasColumnName("user_id");
      entity.Property(session => session.TokenHash)
              .HasColumnName("token_hash")
              .HasMaxLength(128)
              .IsRequired();
      entity.Property(session => session.FamilyId).HasColumnName("family_id");
      entity.Property(session => session.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
      entity.Property(session => session.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp with time zone")
              .HasDefaultValueSql("CURRENT_TIMESTAMP");
      entity.Property(session => session.ExpiresAt)
              .HasColumnName("expires_at")
              .HasColumnType("timestamp with time zone");
      entity.Property(session => session.RevokedAt)
              .HasColumnName("revoked_at")
              .HasColumnType("timestamp with time zone");

      entity.HasIndex(session => session.TokenHash)
              .HasDatabaseName("ux_refresh_sessions_token_hash")
              .IsUnique();
      entity.HasIndex(session => session.FamilyId)
              .HasDatabaseName("ix_refresh_sessions_family_id");
      entity.HasIndex(session => session.ReplacedByTokenId)
              .HasDatabaseName("ux_refresh_sessions_replaced_by_token_id")
              .IsUnique();

      entity.HasOne(session => session.User)
              .WithMany(user => user.RefreshSessions)
              .HasForeignKey(session => session.UserId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_refresh_sessions_users_user_id");

      entity.HasOne(session => session.ReplacedByToken)
          .WithOne()
          .HasForeignKey<RefreshSession>(session => session.ReplacedByTokenId)
          .OnDelete(DeleteBehavior.NoAction)
          .HasConstraintName("fk_refresh_sessions_replacement");
    });
  }

  private static void ConfigureApodEntries(ModelBuilder builder)
  {
    builder.Entity<ApodEntry>(entity =>
    {
      entity.ToTable("apod_entries", tableBuilder =>
              tableBuilder.HasCheckConstraint(
                  "ck_apod_entries_media_type",
                  "media_type IN ('image', 'video')"));

      entity.HasKey(entry => entry.Date)
              .HasName("pk_apod_entries");

      entity.Property(entry => entry.Date).HasColumnName("date");
      entity.Property(entry => entry.Title)
              .HasColumnName("title")
              .IsRequired();
      entity.Property(entry => entry.Explanation)
              .HasColumnName("explanation")
              .IsRequired();
      entity.Property(entry => entry.MediaType)
              .HasColumnName("media_type")
              .HasMaxLength(16)
              .IsRequired();
      entity.Property(entry => entry.Url)
              .HasColumnName("url")
              .IsRequired();
      entity.Property(entry => entry.HdUrl).HasColumnName("hdurl");
      entity.Property(entry => entry.ThumbnailUrl).HasColumnName("thumbnail_url");
      entity.Property(entry => entry.Copyright).HasColumnName("copyright");
      entity.Property(entry => entry.SearchVector)
              .HasColumnName("search_vector")
              .HasColumnType("tsvector")
              .HasComputedColumnSql(
                  "setweight(to_tsvector('english'::regconfig, coalesce(title, '')), 'A') || " +
                  "setweight(to_tsvector('english'::regconfig, coalesce(explanation, '')), 'B')",
                  stored: true);
      entity.Property(entry => entry.CachedAt)
              .HasColumnName("cached_at")
              .HasColumnType("timestamp with time zone")
              .HasDefaultValueSql("CURRENT_TIMESTAMP");

      entity.HasIndex(entry => entry.SearchVector)
              .HasDatabaseName("ix_apod_entries_search_vector")
              .HasMethod("GIN");
    });
  }

  private static void ConfigureFavorites(ModelBuilder builder)
  {
    builder.Entity<Favorite>(entity =>
    {
      entity.ToTable("favorites");

      entity.HasKey(favorite => new { favorite.UserId, favorite.ApodDate })
              .HasName("pk_favorites");

      entity.Property(favorite => favorite.UserId).HasColumnName("user_id");
      entity.Property(favorite => favorite.ApodDate).HasColumnName("apod_date");
      entity.Property(favorite => favorite.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp with time zone")
              .HasDefaultValueSql("CURRENT_TIMESTAMP");

      entity.HasOne(favorite => favorite.User)
              .WithMany(user => user.Favorites)
              .HasForeignKey(favorite => favorite.UserId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_favorites_users_user_id");

      entity.HasOne(favorite => favorite.ApodEntry)
              .WithMany(entry => entry.Favorites)
              .HasForeignKey(favorite => favorite.ApodDate)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_favorites_apod_entries_apod_date");
    });
  }

  private static void ConfigureCatalogSyncStates(ModelBuilder builder)
  {
    builder.Entity<CatalogSyncState>(entity =>
    {
      entity.ToTable("catalog_sync_state", tableBuilder =>
      {
        tableBuilder.HasCheckConstraint(
          "ck_catalog_sync_state_target_range",
          "target_from <= target_to");
        tableBuilder.HasCheckConstraint(
          "ck_catalog_sync_state_checkpoint",
          "last_completed_date IS NULL OR " +
          "(last_completed_date >= target_from AND last_completed_date <= target_to)");
        tableBuilder.HasCheckConstraint(
          "ck_catalog_sync_state_status",
          "status IN ('Pending', 'Running', 'Paused', 'Completed', 'Failed')");
        tableBuilder.HasCheckConstraint(
          "ck_catalog_sync_state_synced_entry_count",
          "synced_entry_count >= 0");
        tableBuilder.HasCheckConstraint(
          "ck_catalog_sync_state_updated_at",
          "updated_at >= created_at");
      });

      entity.HasKey(state => state.Id)
              .HasName("pk_catalog_sync_state");

      entity.Property(state => state.Id).HasColumnName("id");
      entity.Property(state => state.TargetFrom).HasColumnName("target_from");
      entity.Property(state => state.TargetTo).HasColumnName("target_to");
      entity.Property(state => state.LastCompletedDate).HasColumnName("last_completed_date");
      entity.Property(state => state.SyncedEntryCount)
              .HasColumnName("synced_entry_count")
              .HasDefaultValue(0);
      entity.Property(state => state.Status)
              .HasColumnName("status")
              .HasConversion<string>()
              .HasMaxLength(16)
              .HasDefaultValue(CatalogSyncStatus.Pending);
      entity.Property(state => state.LastError).HasColumnName("last_error");
      entity.Property(state => state.RetryNotBefore)
              .HasColumnName("retry_not_before")
              .HasColumnType("timestamp with time zone");
      entity.Property(state => state.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp with time zone")
              .HasDefaultValueSql("CURRENT_TIMESTAMP");
      entity.Property(state => state.UpdatedAt)
              .HasColumnName("updated_at")
              .HasColumnType("timestamp with time zone")
              .HasDefaultValueSql("CURRENT_TIMESTAMP");

      entity.HasIndex(state => new { state.TargetFrom, state.TargetTo })
              .HasDatabaseName("ux_catalog_sync_state_target_range")
              .IsUnique();
    });
  }
}
