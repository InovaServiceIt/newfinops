FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY backend/*.csproj ./
RUN dotnet restore

COPY backend/ ./
RUN dotnet build -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/build .

EXPOSE 5000
ENTRYPOINT ["dotnet", "Predict.Backend.dll"]
