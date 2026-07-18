using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstronomyExplorer.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260717233000_AddCatalogSyncProgress")]
public partial class AddCatalogSyncProgress : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<DateTimeOffset>(
      name: "retry_not_before",
      table: "catalog_sync_state",
      type: "timestamp with time zone",
      nullable: true);

    migrationBuilder.AddColumn<int>(
      name: "synced_entry_count",
      table: "catalog_sync_state",
      type: "integer",
      nullable: false,
      defaultValue: 0);

    migrationBuilder.AddCheckConstraint(
      name: "ck_catalog_sync_state_synced_entry_count",
      table: "catalog_sync_state",
      sql: "synced_entry_count >= 0");
  }

  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropCheckConstraint(
      name: "ck_catalog_sync_state_synced_entry_count",
      table: "catalog_sync_state");

    migrationBuilder.DropColumn(
      name: "retry_not_before",
      table: "catalog_sync_state");

    migrationBuilder.DropColumn(
      name: "synced_entry_count",
      table: "catalog_sync_state");
  }
}
