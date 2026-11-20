# cloudvista-worker (.NET 8 Worker Service + Quartz.NET). Sem porta HTTP.
# Build context: raiz do repositório. Dockerfile: worker/Dockerfile

# ── Build ────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY worker/CloudVista.Worker.csproj worker/
RUN dotnet restore worker/CloudVista.Worker.csproj

COPY worker/ worker/
RUN dotnet publish worker/CloudVista.Worker.csproj -c Release -o /app/publish --no-restore

# ── Runtime ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# Base de fusos (cron das agendas em America/Sao_Paulo).
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Production \
    TZ=America/Sao_Paulo

# Usuário sem privilégios (existe nas imagens .NET 8).
USER app

# ConnectionStrings__CloudVista (svc_cv_worker) entra por variável do Coolify.
ENTRYPOINT ["dotnet", "CloudVista.Worker.dll"]
