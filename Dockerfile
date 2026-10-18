# syntax=docker/dockerfile:1

# ---- build: restaura (com cache de camadas) e copia o codigo ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src/backend
COPY backend/TurnoSync.slnx backend/Directory.Build.props backend/.editorconfig ./
COPY backend/src/TurnoSync.Domain/TurnoSync.Domain.csproj src/TurnoSync.Domain/
COPY backend/src/TurnoSync.Application/TurnoSync.Application.csproj src/TurnoSync.Application/
COPY backend/src/TurnoSync.Infra/TurnoSync.Infra.csproj src/TurnoSync.Infra/
COPY backend/src/TurnoSync.Api/TurnoSync.Api.csproj src/TurnoSync.Api/
COPY backend/tests/TurnoSync.Tests/TurnoSync.Tests.csproj tests/TurnoSync.Tests/
RUN dotnet restore TurnoSync.slnx
COPY backend/ ./

# ---- test: docker-compose run --rm tests ----
FROM build AS test
ENTRYPOINT ["dotnet", "test", "TurnoSync.slnx", "--no-restore"]

# ---- publish: API em Release ----
FROM build AS publish
RUN dotnet publish src/TurnoSync.Api/TurnoSync.Api.csproj -c Release -o /app/publish --no-restore

# ---- final: runtime da API (porta 8080) ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish ./
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TurnoSync.Api.dll"]
