using AstronomyExplorer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AstronomyExplorer.Api.Apod;

public sealed class ApodSearchService(AppDbContext dbContext)
{
  public async Task<IReadOnlyList<ApodEntryDto>> SearchAsync(
    string query,
    int page,
    int pageSize,
    CancellationToken cancellationToken)
  {
    var offset = checked((page - 1) * pageSize);

    return await dbContext.ApodEntries
      .AsNoTracking()
      .Where(entry => entry.SearchVector.Matches(
        EF.Functions.WebSearchToTsQuery("english", query)))
      .OrderByDescending(entry => entry.SearchVector.Rank(
        EF.Functions.WebSearchToTsQuery("english", query)))
      .ThenByDescending(entry => entry.Date)
      .Skip(offset)
      .Take(pageSize)
      .Select(entry => new ApodEntryDto(
        entry.Date,
        entry.Title,
        entry.Explanation,
        entry.MediaType,
        entry.Url,
        entry.HdUrl,
        entry.ThumbnailUrl,
        entry.Copyright))
      .ToListAsync(cancellationToken);
  }
}
