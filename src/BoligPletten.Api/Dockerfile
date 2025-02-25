# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files first for better layer caching
COPY ["src/BoligPletten.Api/BoligPletten.Api.csproj", "src/BoligPletten.Api/"]
COPY ["src/BoligPletten.Core/BoligPletten.Application/BoligPletten.Application.csproj", "src/BoligPletten.Core/BoligPletten.Application/"]
COPY ["src/BoligPletten.Core/BoligPletten.Domain/BoligPletten.Domain.csproj", "src/BoligPletten.Core/BoligPletten.Domain/"]
COPY ["src/BoligPletten.Core/BoligPletten.Infrastructure/BoligPletten.Infrastructure.csproj", "src/BoligPletten.Core/BoligPletten.Infrastructure/"]

# Restore packages
RUN dotnet restore "src/BoligPletten.Api/BoligPletten.Api.csproj"

# Copy the rest of the source code
COPY ["src/BoligPletten.Api/", "src/BoligPletten.Api/"]
COPY ["src/BoligPletten.Core/", "src/BoligPletten.Core/"]

# Build the API project
WORKDIR "/src/src/BoligPletten.Api"
RUN dotnet build "./BoligPletten.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BoligPletten.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoligPletten.Api.dll"]