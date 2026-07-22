FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AstronomyExplorer.Api/AstronomyExplorer.Api.csproj AstronomyExplorer.Api/
RUN dotnet restore AstronomyExplorer.Api/AstronomyExplorer.Api.csproj

COPY AstronomyExplorer.Api/ AstronomyExplorer.Api/
RUN dotnet publish AstronomyExplorer.Api/AstronomyExplorer.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
COPY AstronomyExplorer.Api/docker-entrypoint.sh ./docker-entrypoint.sh

# Checkouts created on Windows can retain CRLF. Normalize inside the Linux image before
# the script becomes executable, avoiding a `^M` interpreter failure at container start.
RUN sed -i 's/\r$//' ./docker-entrypoint.sh \
    && chmod 0555 ./docker-entrypoint.sh

EXPOSE 8080

# The runtime image supplies the unprivileged `app` user. The health probe uses the
# application's DB connectivity path, so API health remains meaningful without curl.
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
  CMD ["/app/docker-entrypoint.sh", "--healthcheck"]

ENTRYPOINT ["/app/docker-entrypoint.sh"]
