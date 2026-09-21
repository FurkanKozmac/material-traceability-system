# Multi-stage Dockerfile for .NET 10 REST API

# --- Stage 1: Build & Publish ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Cache dependency restoration
COPY ["backend.csproj", "./"]
RUN dotnet restore "backend.csproj"

# Copy source code and publish
COPY . .
RUN dotnet publish "backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Non-root user execution for security
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5059
EXPOSE 5059

ENTRYPOINT ["dotnet", "backend.dll"]
