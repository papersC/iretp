# ----------------------------------------------------------------------------
# IRETP Internal AdminAPI — hardened multi-stage Docker build.
#   docker build -f src/IRETP.AdminAPI/Dockerfile -t iretp/adminapi .
# ----------------------------------------------------------------------------
ARG DOTNET_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY IRETP.sln ./
COPY src/IRETP.Domain/*.csproj           src/IRETP.Domain/
COPY src/IRETP.Application/*.csproj      src/IRETP.Application/
COPY src/IRETP.Infrastructure/*.csproj   src/IRETP.Infrastructure/
COPY src/IRETP.WebAPI/*.csproj           src/IRETP.WebAPI/
COPY src/IRETP.AdminAPI/*.csproj         src/IRETP.AdminAPI/
COPY src/IRETP.Web/*.csproj              src/IRETP.Web/
RUN dotnet restore src/IRETP.AdminAPI/IRETP.AdminAPI.csproj

COPY src/ src/
RUN dotnet publish src/IRETP.AdminAPI/IRETP.AdminAPI.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos "" --uid 10002 iretp

COPY --from=build --chown=iretp:iretp /app/publish ./

ENV ASPNETCORE_URLS=http://+:5002
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0
EXPOSE 5002

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:5002/healthz || exit 1

USER iretp

ENTRYPOINT ["dotnet", "IRETP.AdminAPI.dll"]
