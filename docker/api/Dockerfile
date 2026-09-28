# syntax=docker/dockerfile:1.7
# Image lodb-api: the ASP.NET Core host and its sub-commands (healthcheck, then migrate…).
# Build context = repository root; Dockerfile.dockerignore (next to this file) admits only
# the solution-wide build settings and the four server projects.

# The SDK runs on the build machine and cross-publishes for the target architecture: the
# output is IL plus the target's native assets, so no emulation is needed.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /source

# Project files first: the restore layer survives every source-only change.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/LoDb.Domain/LoDb.Domain.csproj src/LoDb.Domain/
COPY src/LoDb.Infrastructure/LoDb.Infrastructure.csproj src/LoDb.Infrastructure/
COPY src/LoDb.Ingestion/LoDb.Ingestion.csproj src/LoDb.Ingestion/
COPY src/LoDb.Api/LoDb.Api.csproj src/LoDb.Api/
RUN --mount=type=cache,id=lodb-nuget,target=/root/.nuget/packages \
    dotnet restore src/LoDb.Api/LoDb.Api.csproj -a "$TARGETARCH"

COPY src/ src/
RUN --mount=type=cache,id=lodb-nuget,target=/root/.nuget/packages \
    dotnet publish src/LoDb.Api/LoDb.Api.csproj -c Release -a "$TARGETARCH" --no-restore \
        -p:UseAppHost=false -o /out/app \
    # The storage mount point, created here because the runtime image has no shell. Docker
    # seeds an empty named volume from the image, ownership included: the API can then
    # write to a fresh volume without any chown step.
    && mkdir -p /out/storage

# Chiseled: no shell, no package manager, non-root by default. "extra" adds ICU and tzdata,
# which the 21 locales need (culture-aware formatting and collation).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
ARG APP_REVISION=unknown
LABEL org.opencontainers.image.title="lodb-api" \
      org.opencontainers.image.source="https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal" \
      org.opencontainers.image.revision="${APP_REVISION}"
ENV APP_REVISION=${APP_REVISION} \
    LoDb__Storage__Root=/srv/storage
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /out/storage /srv/storage
COPY --from=build /out/app ./
USER $APP_UID
# 8080: the API, reached by nginx and the SSR. 9464: /metrics only, never published.
EXPOSE 8080 9464
ENTRYPOINT ["dotnet", "LoDb.Api.dll"]
