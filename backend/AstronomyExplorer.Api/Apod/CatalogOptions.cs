using Microsoft.Extensions.Options;

namespace AstronomyExplorer.Api.Apod;

public sealed class CatalogOptions
{
  public const string SectionName = "Catalog";

  public DateOnly? RequiredFrom { get; init; }

  public DateOnly? RequiredTo { get; init; }
}

public sealed class CatalogOptionsValidator(IApodProductCalendar calendar)
  : IValidateOptions<CatalogOptions>
{
  public ValidateOptionsResult Validate(string? name, CatalogOptions options)
  {
    if (options.RequiredFrom is null && options.RequiredTo is null)
    {
      return ValidateOptionsResult.Success;
    }

    var latestAvailableDate = calendar.GetLatestAvailableDate();
    if (options.RequiredFrom is null ||
        options.RequiredTo is null ||
        options.RequiredFrom < ApodEndpoints.FirstApodDate ||
        options.RequiredFrom > options.RequiredTo ||
        options.RequiredTo > latestAvailableDate)
    {
      return ValidateOptionsResult.Fail(
        "Catalog:RequiredFrom and Catalog:RequiredTo must define a valid APOD range through the current APOD product date.");
    }

    return ValidateOptionsResult.Success;
  }
}
