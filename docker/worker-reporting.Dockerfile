FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
#
COPY Daraban.Platform.sln Directory.Packages.props ./
COPY src/ src/
#
RUN dotnet restore src/Workers/Daraban.Workers.Reporting/Daraban.Workers.Reporting.csproj
RUN dotnet publish src/Workers/Daraban.Workers.Reporting/Daraban.Workers.Reporting.csproj -c Release -o /app/publish --no-restore
#
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
#
COPY --from=build /app/publish .
# Report artifacts are written here; mount a volume (see docker-compose worker-reporting).
VOLUME ["/var/daraban/reports"]
ENV ReportStore__RootPath=/var/daraban/reports
ENTRYPOINT ["dotnet", "Daraban.Workers.Reporting.dll"]
