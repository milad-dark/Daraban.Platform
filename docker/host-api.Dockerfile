FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
#
COPY Daraban.Platform.sln Directory.Packages.props ./
COPY src/ src/
COPY Daraban.Platform.Messaging/ Daraban.Platform.Messaging/
#
RUN dotnet restore src/Hosts/Daraban.Host.Api/Daraban.Host.Api.csproj
RUN dotnet publish src/Hosts/Daraban.Host.Api/Daraban.Host.Api.csproj -c Release -o /app/publish --no-restore
#
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
#
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
#
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Daraban.Host.Api.dll"]
