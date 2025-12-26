# Production-ready multi-stage Dockerfile for Faunex.Api (ASP.NET Core Web API, .NET 10)

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution file first for better layer caching
COPY *.slnx ./

# Copy project files (for restore layer)
COPY Faunex.Api/Faunex.Api.csproj Faunex.Api/
COPY Faunex.Application/Faunex.Application.csproj Faunex.Application/
COPY Faunex.Infrastructure/Faunex.Infrastructure.csproj Faunex.Infrastructure/
COPY Faunex.Domain/Faunex.Domain.csproj Faunex.Domain/
COPY Faunex.Shared/Faunex.Shared.csproj Faunex.Shared/

# Restore dependencies
RUN dotnet restore Faunex.Api/Faunex.Api.csproj

# Copy remaining source
COPY . .

# Publish
RUN dotnet publish Faunex.Api/Faunex.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Faunex.Api.dll"]
