# Build context: c:/Sarvar-Apps/FT/development-tg-bot
#   docker build -t development-tg-bot .
#
# Multi-stage so the final image ships only the runtime + published
# bits — the SDK layer (~700MB) is discarded.

# ───────────── Build stage ─────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore first, in its own layer, so a source-only edit doesn't
# invalidate the NuGet cache and re-download every package.
COPY ["development-tg-bot.csproj", "./"]
RUN dotnet restore "development-tg-bot.csproj"

COPY . .
RUN dotnet publish "development-tg-bot.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ───────────── Runtime stage ─────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Plain HTTP inside the container; TLS terminates at the reverse proxy
# (nginx / traefik) outside. 8080 mirrors the convention used by the
# other services in this repo.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080

COPY --from=build /app/publish .

# $APP_UID is preset by the aspnet base image (uid 1654). Running as
# non-root limits the blast radius if the process gets exploited.
RUN chown -R $APP_UID:$APP_UID /app
USER $APP_UID

ENTRYPOINT ["dotnet", "DevelopmentTgBot.dll"]
