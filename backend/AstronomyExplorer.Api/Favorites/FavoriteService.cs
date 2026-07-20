using AstronomyExplorer.Api.Apod;
using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Favorites;

public sealed class FavoriteService(AppDbContext dbContext)
{
  public async Task<IReadOnlyList<ApodEntryDto>> GetAllAsync(
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await dbContext.Favorites
      .AsNoTracking()
      .Where(favorite => favorite.UserId == userId)
      .OrderByDescending(favorite => favorite.ApodDate)
      .Select(favorite => new ApodEntryDto(
        favorite.ApodEntry.Date,
        favorite.ApodEntry.Title,
        favorite.ApodEntry.Explanation,
        favorite.ApodEntry.MediaType,
        favorite.ApodEntry.Url,
        favorite.ApodEntry.HdUrl,
        favorite.ApodEntry.ThumbnailUrl,
        favorite.ApodEntry.Copyright))
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(
    Guid userId,
    DateOnly apodDate,
    DateTimeOffset createdAt,
    CancellationToken cancellationToken)
  {
    await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
      INSERT INTO favorites (user_id, apod_date, created_at)
      VALUES ({{userId}}, {{apodDate}}, {{createdAt}})
      ON CONFLICT (user_id, apod_date) DO NOTHING
      """, cancellationToken);
  }

  public async Task RemoveAsync(
    Guid userId,
    DateOnly apodDate,
    CancellationToken cancellationToken)
  {
    await dbContext.Favorites
      .Where(favorite => favorite.UserId == userId && favorite.ApodDate == apodDate)
      .ExecuteDeleteAsync(cancellationToken);
  }
}
