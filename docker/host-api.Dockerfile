# =============================================================================
# Daraban.Host.Api � CI unit tests: docker build --target test -f docker/host-api.Dockerfile .
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base
WORKDIR /src
COPY Daraban.Platform.sln Directory.Build.props Directory.Packages.props ./
COPY src/ src/
COPY tests/ tests/
COPY tools/ tools/

FROM base AS restore
RUN dotnet restore Daraban.Platform.sln

FROM restore AS build
COPY --from=restore /root/.nuget/packages /root/.nuget/packages
RUN dotnet build Daraban.Platform.sln -c Release --no-restore

FROM build AS test
RUN dotnet test Daraban.Platform.sln -c Release --no-build --filter "FullyQualifiedName!~Daraban.IntegrationTests"

FROM build AS publish
RUN dotnet publish src/Host/Daraban.Host.Api/Daraban.Host.Api.csproj -c Release -o /app/publish --no-build --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl is used by the compose healthcheck below.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Daraban.Host.Api.dll"]