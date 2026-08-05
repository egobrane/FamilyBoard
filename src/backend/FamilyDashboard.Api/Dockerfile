FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /workspace
COPY global.json Directory.Build.props ./
COPY src/backend/FamilyDashboard.Api/FamilyDashboard.Api.csproj src/backend/FamilyDashboard.Api/
RUN dotnet restore src/backend/FamilyDashboard.Api/FamilyDashboard.Api.csproj
COPY . .

FROM development AS build
RUN dotnet publish src/backend/FamilyDashboard.Api/FamilyDashboard.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS production
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "FamilyDashboard.Api.dll"]
