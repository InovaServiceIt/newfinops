# ── Stage 1: Build Frontend (React/Vite) ─────────────────────────────────────
FROM node:20-alpine AS frontend-build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ── Stage 2: Build Backend (.NET 8) ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY backend/Finops.Backend.csproj ./
RUN dotnet restore

COPY backend/ ./

# Copiar o build do frontend para wwwroot do backend
COPY --from=frontend-build /app/dist ./wwwroot

RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 3: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=backend-build /app/publish .

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Segredos e configuração (AzureAd__*, Cors__AllowedOrigins) entram por variáveis do Coolify.
ENTRYPOINT ["dotnet", "Finops.Backend.dll"]
