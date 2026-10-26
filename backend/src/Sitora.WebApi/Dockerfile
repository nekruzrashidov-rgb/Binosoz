# Продакшен-образ: многоэтапная сборка, в итоговом образе только рантайм ASP.NET и опубликованное приложение,
# процесс работает не от root. Сборка из папки backend/: docker build -f src/Sitora.WebApi/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Sitora.Domain/Sitora.Domain.csproj src/Sitora.Domain/
COPY src/Sitora.Infrastructure/Sitora.Infrastructure.csproj src/Sitora.Infrastructure/
COPY src/Sitora.WebApi/Sitora.WebApi.csproj src/Sitora.WebApi/
RUN dotnet restore src/Sitora.WebApi/Sitora.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/Sitora.WebApi/Sitora.WebApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Storage__RootPath=/app/storage
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/storage /app/logs && chown $APP_UID /app/storage /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "Sitora.WebApi.dll"]
