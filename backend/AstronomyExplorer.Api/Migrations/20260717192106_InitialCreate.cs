using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace AstronomyExplorer.Api.Migrations
{
  /// <inheritdoc />
  public partial class InitialCreate : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "apod_entries",
          columns: table => new
          {
            date = table.Column<DateOnly>(type: "date", nullable: false),
            title = table.Column<string>(type: "text", nullable: false),
            explanation = table.Column<string>(type: "text", nullable: false),
            media_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            url = table.Column<string>(type: "text", nullable: false),
            hdurl = table.Column<string>(type: "text", nullable: true),
            thumbnail_url = table.Column<string>(type: "text", nullable: true),
            copyright = table.Column<string>(type: "text", nullable: true),
            search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false, computedColumnSql: "setweight(to_tsvector('english'::regconfig, coalesce(title, '')), 'A') || setweight(to_tsvector('english'::regconfig, coalesce(explanation, '')), 'B')", stored: true),
            cached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_apod_entries", x => x.date);
            table.CheckConstraint("ck_apod_entries_media_type", "media_type IN ('image', 'video')");
          });

      migrationBuilder.CreateTable(
          name: "AspNetUsers",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
            PasswordHash = table.Column<string>(type: "text", nullable: true),
            SecurityStamp = table.Column<string>(type: "text", nullable: true),
            ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
            PhoneNumber = table.Column<string>(type: "text", nullable: true),
            PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
            TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
            LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
            AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_AspNetUsers", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "catalog_sync_state",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            target_from = table.Column<DateOnly>(type: "date", nullable: false),
            target_to = table.Column<DateOnly>(type: "date", nullable: false),
            last_completed_date = table.Column<DateOnly>(type: "date", nullable: true),
            status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
            last_error = table.Column<string>(type: "text", nullable: true),
            created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
            updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_catalog_sync_state", x => x.id);
            table.CheckConstraint("ck_catalog_sync_state_checkpoint", "last_completed_date IS NULL OR (last_completed_date >= target_from AND last_completed_date <= target_to)");
            table.CheckConstraint("ck_catalog_sync_state_status", "status IN ('Pending', 'Running', 'Paused', 'Completed', 'Failed')");
            table.CheckConstraint("ck_catalog_sync_state_target_range", "target_from <= target_to");
            table.CheckConstraint("ck_catalog_sync_state_updated_at", "updated_at >= created_at");
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserClaims",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            ClaimType = table.Column<string>(type: "text", nullable: true),
            ClaimValue = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
            table.ForeignKey(
                      name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                      column: x => x.UserId,
                      principalTable: "AspNetUsers",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserLogins",
          columns: table => new
          {
            LoginProvider = table.Column<string>(type: "text", nullable: false),
            ProviderKey = table.Column<string>(type: "text", nullable: false),
            ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
            UserId = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
            table.ForeignKey(
                      name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                      column: x => x.UserId,
                      principalTable: "AspNetUsers",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserTokens",
          columns: table => new
          {
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            LoginProvider = table.Column<string>(type: "text", nullable: false),
            Name = table.Column<string>(type: "text", nullable: false),
            Value = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
            table.ForeignKey(
                      name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                      column: x => x.UserId,
                      principalTable: "AspNetUsers",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "favorites",
          columns: table => new
          {
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            apod_date = table.Column<DateOnly>(type: "date", nullable: false),
            created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_favorites", x => new { x.user_id, x.apod_date });
            table.ForeignKey(
                      name: "fk_favorites_apod_entries_apod_date",
                      column: x => x.apod_date,
                      principalTable: "apod_entries",
                      principalColumn: "date",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_favorites_users_user_id",
                      column: x => x.user_id,
                      principalTable: "AspNetUsers",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "refresh_sessions",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            family_id = table.Column<Guid>(type: "uuid", nullable: false),
            replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
            created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
            expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_refresh_sessions", x => x.id);
            table.CheckConstraint("ck_refresh_sessions_expiry", "expires_at > created_at");
            table.CheckConstraint("ck_refresh_sessions_replacement", "replaced_by_token_id IS NULL OR replaced_by_token_id <> id");
            table.CheckConstraint("ck_refresh_sessions_revocation", "revoked_at IS NULL OR revoked_at >= created_at");
            table.ForeignKey(
                      name: "fk_refresh_sessions_replacement",
                      column: x => x.replaced_by_token_id,
                      principalTable: "refresh_sessions",
                      principalColumn: "id");
            table.ForeignKey(
                      name: "fk_refresh_sessions_users_user_id",
                      column: x => x.user_id,
                      principalTable: "AspNetUsers",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "ix_apod_entries_search_vector",
          table: "apod_entries",
          column: "search_vector")
          .Annotation("Npgsql:IndexMethod", "GIN");

      migrationBuilder.CreateIndex(
          name: "IX_AspNetUserClaims_UserId",
          table: "AspNetUserClaims",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_AspNetUserLogins_UserId",
          table: "AspNetUserLogins",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "EmailIndex",
          table: "AspNetUsers",
          column: "NormalizedEmail",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "UserNameIndex",
          table: "AspNetUsers",
          column: "NormalizedUserName",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ux_catalog_sync_state_target_range",
          table: "catalog_sync_state",
          columns: new[] { "target_from", "target_to" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_favorites_apod_date",
          table: "favorites",
          column: "apod_date");

      migrationBuilder.CreateIndex(
          name: "ix_refresh_sessions_family_id",
          table: "refresh_sessions",
          column: "family_id");

      migrationBuilder.CreateIndex(
          name: "IX_refresh_sessions_user_id",
          table: "refresh_sessions",
          column: "user_id");

      migrationBuilder.CreateIndex(
          name: "ux_refresh_sessions_replaced_by_token_id",
          table: "refresh_sessions",
          column: "replaced_by_token_id",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ux_refresh_sessions_token_hash",
          table: "refresh_sessions",
          column: "token_hash",
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "AspNetUserClaims");

      migrationBuilder.DropTable(
          name: "AspNetUserLogins");

      migrationBuilder.DropTable(
          name: "AspNetUserTokens");

      migrationBuilder.DropTable(
          name: "catalog_sync_state");

      migrationBuilder.DropTable(
          name: "favorites");

      migrationBuilder.DropTable(
          name: "refresh_sessions");

      migrationBuilder.DropTable(
          name: "apod_entries");

      migrationBuilder.DropTable(
          name: "AspNetUsers");
    }
  }
}
