# ----------------------------------------------------------------------------
# IRETP Public WebAPI — hardened multi-stage Docker build.
# Builds from the repo root as context so ProjectReferences resolve against
# sibling projects.
#   docker build -f src/IRETP.WebAPI/Dockerfile -t iretp/webapi .
# ----------------------------------------------------------------------------
ARG DOTNET_VERSION=9.0

# --- Build stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first so the cache is warm across source-only edits.
COPY IRETP.sln ./
COPY src/IRETP.Domain/*.csproj           src/IRETP.Domain/
COPY src/IRETP.Application/*.csproj      src/IRETP.Application/
COPY src/IRETP.Infrastructure/*.csproj   src/IRETP.Infrastructure/
COPY src/IRETP.WebAPI/*.csproj           src/IRETP.WebAPI/
COPY src/IRETP.AdminAPI/*.csproj         src/IRETP.AdminAPI/
COPY src/IRETP.Web/*.csproj              src/IRETP.Web/
RUN dotnet restore src/IRETP.WebAPI/IRETP.WebAPI.csproj

COPY src/ src/
RUN dotnet publish src/IRETP.WebAPI/IRETP.WebAPI.csproj \
    -c Release --no-restore -o /app/publish

# --- Runtime stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# curl for HEALTHCHECK; --no-install-recommends keeps the layer small.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos "" --uid 10001 iretp

COPY --from=build --chown=iretp:iretp /app/publish ./

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0
EXPOSE 5000

# RFP §10.1 99.9% uptime — Docker-level liveness so orchestrators can
# restart the container on stuck workers before users notice.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:5000/healthz/live || exit 1

USER iretp

ENTRYPOINT ["dotnet", "IRETP.WebAPI.dll"]
