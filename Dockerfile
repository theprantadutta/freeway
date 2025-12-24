# Freeway API - .NET 10 Clean Architecture
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y libkrb5-3 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files
COPY ["Freeway.sln", "./"]
COPY ["src/Freeway.Domain/Freeway.Domain.csproj", "src/Freeway.Domain/"]
COPY ["src/Freeway.Application/Freeway.Application.csproj", "src/Freeway.Application/"]
COPY ["src/Freeway.Infrastructure/Freeway.Infrastructure.csproj", "src/Freeway.Infrastructure/"]
COPY ["src/Freeway.Api/Freeway.Api.csproj", "src/Freeway.Api/"]

# Restore dependencies
RUN dotnet restore "Freeway.sln"

# Copy all source code
COPY . .

# Build the solution
WORKDIR "/src/src/Freeway.Api"
RUN dotnet build "Freeway.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Freeway.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Freeway.Api.dll"]
