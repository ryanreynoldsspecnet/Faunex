# Production-ready multi-stage Dockerfile for Faunex.Api (ASP.NET Core Web API, .NET 10)

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution file first for better layer caching
COPY *.sln ./

# Copy only the API project file and restore its dependencies
COPY Faunex.Api.csproj ./
RUN dotnet restore Faunex.Api.csproj

# Copy remaining source
COPY . .

# Publish
RUN dotnet publish Faunex.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Faunex.Api.dll"]
