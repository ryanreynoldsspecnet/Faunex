# Production-ready multi-stage Dockerfile for Faunex.Web (Blazor, .NET 10)

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution file first for better layer caching
COPY *.slnx ./

# Copy project files (for restore layer)
COPY Faunex.Web/Faunex.Web.csproj Faunex.Web/
COPY Faunex.Application/Faunex.Application.csproj Faunex.Application/
COPY Faunex.Domain/Faunex.Domain.csproj Faunex.Domain/
COPY Faunex.Shared/Faunex.Shared.csproj Faunex.Shared/

# Restore dependencies
RUN dotnet restore Faunex.Web/Faunex.Web.csproj

# Copy remaining source
COPY . .

# Publish
RUN dotnet publish Faunex.Web/Faunex.Web.csproj \
    -c Release \
    -o /app/publish \
    -p:StaticWebAssetsMode=Publish \
    --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
ARG RELEASE_REVISION=development
LABEL org.opencontainers.image.revision=$RELEASE_REVISION
ENV ReleaseRevision=$RELEASE_REVISION

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Faunex.Web.dll"]
