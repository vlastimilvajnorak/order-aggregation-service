# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /source

# Copy only what restore needs first, so the package layer stays cached until the
# project file or the central package versions actually change.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrderAggregationService/OrderAggregationService.csproj src/OrderAggregationService/
RUN dotnet restore src/OrderAggregationService/OrderAggregationService.csproj

# .editorconfig is copied so the analyzers behave exactly as they do locally.
COPY .editorconfig ./
COPY src/ src/

RUN dotnet publish src/OrderAggregationService/OrderAggregationService.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-restore \
    --output /app

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl is the only addition to the runtime image and exists solely so the
# container can probe its own health endpoint.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# The .NET runtime images ship a non-root user; APP_UID is provided by the base image.
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "OrderAggregationService.dll"]
