# ----------------------------------------------------------------------------
# IRETP Blazor Server Web — hardened multi-stage Docker build.
#   docker build -f src/IRETP.Web/Dockerfile -t iretp/web .
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
RUN dotnet restore src/IRETP.Web/IRETP.Web.csproj

COPY src/ src/
RUN dotnet publish src/IRETP.Web/IRETP.Web.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos "" --uid 10010 iretp

COPY --from=build --chown=iretp:iretp /app/publish ./

ENV ASPNETCORE_URLS=http://+:5010
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0
EXPOSE 5010

# Hits the Blazor home page — returns 200 when Kestrel is serving.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:5010/ || exit 1

USER iretp

ENTRYPOINT ["dotnet", "IRETP.Web.dll"]
