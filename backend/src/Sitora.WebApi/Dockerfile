# Продакшен-образ: многоэтапная сборка, в итоговом образе только рантайм ASP.NET и опубликованное приложение,
# процесс работает не от root. Сборка из корня репозитория (нужны backend/ и frontend/):
# docker build -f backend/src/Sitora.WebApi/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/Sitora.Domain/Sitora.Domain.csproj src/Sitora.Domain/
COPY backend/src/Sitora.Infrastructure/Sitora.Infrastructure.csproj src/Sitora.Infrastructure/
COPY backend/src/Sitora.WebApi/Sitora.WebApi.csproj src/Sitora.WebApi/
RUN dotnet restore src/Sitora.WebApi/Sitora.WebApi.csproj

COPY backend/src/ src/
RUN dotnet publish src/Sitora.WebApi/Sitora.WebApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Storage__RootPath=/app/storage
EXPOSE 8080
COPY --from=build /app/publish .
# Сайт: страницы, скрипты и стили отдаёт сам API.
COPY frontend/ wwwroot/
RUN mkdir -p /app/storage /app/logs && chown $APP_UID /app/storage /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "Sitora.WebApi.dll"]
