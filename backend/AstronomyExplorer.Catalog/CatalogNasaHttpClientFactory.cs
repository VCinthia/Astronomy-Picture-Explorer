namespace AstronomyExplorer.Catalog;

public static class CatalogNasaHttpClientFactory
{
  public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

  public static HttpClient Create()
  {
    var client = new HttpClient(new HttpClientHandler
    {
      AllowAutoRedirect = false
    })
    {
      BaseAddress = new Uri("https://api.nasa.gov/"),
      Timeout = RequestTimeout
    };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AstronomyExplorer.Catalog/1.0");
    return client;
  }
}
