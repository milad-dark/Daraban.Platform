FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
#
COPY Daraban.Platform.sln Directory.Packages.props ./
COPY src/ src/
COPY Daraban.Platform.Messaging/ Daraban.Platform.Messaging/
#
RUN dotnet restore src/Workers/Daraban.Worker.Automation/Daraban.Worker.Automation.csproj
RUN dotnet publish src/Workers/Daraban.Worker.Automation/Daraban.Worker.Automation.csproj -c Release -o /app/publish --no-restore
#
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
#
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Daraban.Worker.Automation.dll"]
