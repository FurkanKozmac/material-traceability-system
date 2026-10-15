# Multi-stage Dockerfile for .NET 10 REST API

# --- Stage 1: Build & Publish ---
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /src

# Cache dependency restoration
COPY ["backend.csproj", "./"]
RUN dotnet restore "backend.csproj"

# Copy source code and publish
COPY . .
RUN dotnet publish "backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:819bd3649d8bf53f35527c68d62e17b348ef6f24ae73073f497f00a878348062 AS final
WORKDIR /app

# Non-root user execution for security
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5059
EXPOSE 5059

ENTRYPOINT ["dotnet", "backend.dll"]
