# =============================================================================
# Notifications worker (Daraban.Workers.NotificationDispatcher)
# CI unit tests: docker build --target test -f docker/worker-notifications.Dockerfile .
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
RUN dotnet publish src/Workers/Daraban.Workers.NotificationDispatcher/Daraban.Workers.NotificationDispatcher.csproj -c Release -o /app/publish --no-build --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Daraban.Workers.NotificationDispatcher.dll"]